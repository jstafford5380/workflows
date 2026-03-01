using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Engine.Core.Definitions;
using Engine.Core.Execution;
using Engine.Core.Validation;
using Engine.Runtime.Contracts;
using Microsoft.Extensions.Options;

namespace Engine.Api.Bundles;

public sealed class BundleService : IBundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly BundleOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<BundleService> _logger;
    private readonly ConcurrentDictionary<string, BundlePreviewState> _previews = new(StringComparer.OrdinalIgnoreCase);

    public BundleService(
        IOptions<BundleOptions> options,
        IWebHostEnvironment environment,
        ILogger<BundleService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<BundlePreviewResponse> CreatePreviewAsync(IFormFile bundleFile, CancellationToken cancellationToken)
    {
        CleanupExpiredPreviews();

        if (bundleFile.Length <= 0)
        {
            throw new InvalidOperationException("Bundle file is empty.");
        }

        if (bundleFile.Length > _options.MaxUploadBytes)
        {
            throw new InvalidOperationException($"Bundle exceeds max size {_options.MaxUploadBytes} bytes.");
        }

        if (!bundleFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bundle must be a .zip file.");
        }

        var previewId = Guid.NewGuid().ToString("N");
        var previewRoot = Path.Combine(GetPreviewRootPath(), previewId);
        var extractedRoot = Path.Combine(previewRoot, "unpacked");

        Directory.CreateDirectory(extractedRoot);

        await using var stream = bundleFile.OpenReadStream();
        ExtractZipSafely(stream, extractedRoot, cancellationToken);

        var files = Directory.GetFiles(extractedRoot, "*", SearchOption.AllDirectories)
            .Select(path => ToRelativeUnixPath(extractedRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var validationErrors = new List<string>();
        var workflowPath = Path.Combine(extractedRoot, "workflow.json");
        if (!File.Exists(workflowPath))
        {
            validationErrors.Add("Bundle must include 'workflow.json' at the root.");
            return StoreAndCreateResponse(new BundlePreviewState(
                previewId,
                bundleFile.FileName,
                previewRoot,
                extractedRoot,
                new WorkflowDefinition { Name = "invalid", Version = 1, Steps = [] },
                files,
                new Dictionary<string, string>(),
                [],
                [],
                validationErrors,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(_options.PreviewTtlMinutes)));
        }

        var workflowJson = await File.ReadAllTextAsync(workflowPath, cancellationToken);
        WorkflowDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinition>(workflowJson, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to deserialize workflow.json in bundle preview {PreviewId}", previewId);
            throw new InvalidOperationException("workflow.json is invalid JSON for WorkflowDefinition.");
        }

        if (definition is null)
        {
            throw new InvalidOperationException("workflow.json did not deserialize into a workflow definition.");
        }

        var definitionValidation = WorkflowDefinitionValidator.Validate(definition);
        if (!definitionValidation.IsValid)
        {
            validationErrors.AddRange(definitionValidation.Errors);
        }

        var graph = DependencyGraphBuilder.Build(definition);
        if (!graph.IsValid)
        {
            validationErrors.AddRange(graph.Errors);
        }

        var fileSet = files.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scriptPathsByStep = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in definition.Steps)
        {
            if (step.WaitForEvent is not null)
            {
                continue;
            }

            if (!TryResolveBundleScriptPath(step.ActivityRef, out var scriptPath, out var error))
            {
                validationErrors.Add($"Step '{step.StepId}' has invalid activityRef '{step.ActivityRef}': {error}");
                continue;
            }

            scriptPathsByStep[step.StepId] = scriptPath;

            if (!fileSet.Contains(scriptPath))
            {
                validationErrors.Add($"Step '{step.StepId}' references missing script '{scriptPath}'.");
            }
        }

        var stepPreviews = definition.Steps
            .Select(step =>
            {
                var dependencies = graph.Dependencies.TryGetValue(step.StepId, out var deps)
                    ? deps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>();

                var scriptPath = scriptPathsByStep.TryGetValue(step.StepId, out var path) ? path : null;
                return new BundleStepPreview(
                    step.StepId,
                    step.DisplayName,
                    step.ActivityRef,
                    step.WaitForEvent is not null,
                    step.WaitForEvent?.EventType,
                    dependencies,
                    scriptPath,
                    scriptPath is not null && fileSet.Contains(scriptPath));
            })
            .ToList();

        var executionPlan = graph.IsValid
            ? BuildExecutionPlan(graph.Dependencies)
            : [];

        var previewState = new BundlePreviewState(
            previewId,
            bundleFile.FileName,
            previewRoot,
            extractedRoot,
            definition,
            files,
            scriptPathsByStep,
            executionPlan,
            stepPreviews,
            validationErrors,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(_options.PreviewTtlMinutes));

        return StoreAndCreateResponse(previewState);
    }

    public Task<BundlePreviewResponse?> GetPreviewAsync(string previewId, CancellationToken cancellationToken)
    {
        CleanupExpiredPreviews();
        if (!_previews.TryGetValue(previewId, out var state))
        {
            return Task.FromResult<BundlePreviewResponse?>(null);
        }

        return Task.FromResult<BundlePreviewResponse?>(ToResponse(state));
    }

    public async Task<BundleRegisterResponse> RegisterPreviewAsync(
        string previewId,
        IWorkflowEngineService workflowEngine,
        CancellationToken cancellationToken)
    {
        CleanupExpiredPreviews();

        if (!_previews.TryGetValue(previewId, out var preview))
        {
            throw new InvalidOperationException($"Preview '{previewId}' was not found.");
        }

        if (preview.ValidationErrors.Count > 0)
        {
            throw new InvalidOperationException("Bundle preview has validation errors; fix them before registration.");
        }

        var existingDefinition = await workflowEngine.GetWorkflowDefinitionAsync(
            preview.WorkflowDefinition.Name,
            preview.WorkflowDefinition.Version,
            cancellationToken);
        var priorBundleIds = CollectBundleIds(existingDefinition);

        var bundleId = Guid.NewGuid().ToString("N");
        var bundleRoot = Path.Combine(GetStorageRootPath(), bundleId);
        Directory.CreateDirectory(bundleRoot);

        CopyDirectory(preview.ExtractedRoot, bundleRoot);

        var transformedSteps = preview.WorkflowDefinition.Steps
            .Select(step =>
            {
                if (step.WaitForEvent is not null)
                {
                    return step;
                }

                if (!preview.StepScriptPaths.TryGetValue(step.StepId, out var relativeScriptPath))
                {
                    throw new InvalidOperationException($"Missing script mapping for step '{step.StepId}'.");
                }

                return step with
                {
                    ActivityRef = $"bundle://{bundleId}/{relativeScriptPath}"
                };
            })
            .ToList();

        var transformedDefinition = preview.WorkflowDefinition with { Steps = transformedSteps };

        var registrationMetadata = await workflowEngine.RegisterWorkflowDefinitionAsync(transformedDefinition, cancellationToken);

        var metadataPath = Path.Combine(bundleRoot, "bundle-metadata.json");
        var metadata = new
        {
            bundleId,
            preview.BundleFileName,
            registrationMetadata.Name,
            registrationMetadata.Version,
            registrationMetadata.Revision,
            registrationMetadata.RegisteredAt,
            preview.Files
        };

        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);

        foreach (var priorBundleId in priorBundleIds.Where(x => !string.Equals(x, bundleId, StringComparison.OrdinalIgnoreCase)))
        {
            var priorBundleRoot = Path.Combine(GetStorageRootPath(), priorBundleId);
            TryDeleteDirectory(priorBundleRoot);
        }

        CleanupPreview(preview.PreviewId, preview.PreviewRoot);

        return new BundleRegisterResponse(
            bundleId,
            registrationMetadata.Name,
            registrationMetadata.Version,
            registrationMetadata.Revision,
            registrationMetadata.RegisteredAt);
    }

    private BundlePreviewResponse StoreAndCreateResponse(BundlePreviewState state)
    {
        _previews[state.PreviewId] = state;
        return ToResponse(state);
    }

    private static BundlePreviewResponse ToResponse(BundlePreviewState state)
    {
        return new BundlePreviewResponse(
            state.PreviewId,
            state.BundleFileName,
            state.WorkflowDefinition.Name,
            state.WorkflowDefinition.Version,
            state.WorkflowDefinition.Description,
            state.WorkflowDefinition.Details,
            state.WorkflowDefinition.InputSchema,
            state.Steps,
            state.Files,
            state.ExecutionPlan,
            state.ValidationErrors.Count == 0,
            state.ValidationErrors,
            state.ExpiresAt);
    }

    private void CleanupExpiredPreviews()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _previews
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var previewId in expired)
        {
            if (_previews.TryRemove(previewId, out var state))
            {
                TryDeleteDirectory(state.PreviewRoot);
            }
        }
    }

    private void CleanupPreview(string previewId, string previewRoot)
    {
        _previews.TryRemove(previewId, out _);
        TryDeleteDirectory(previewRoot);
    }

    private static IReadOnlyList<ExecutionPlanStage> BuildExecutionPlan(IReadOnlyDictionary<string, IReadOnlySet<string>> dependencies)
    {
        var remaining = dependencies.ToDictionary(
            kvp => kvp.Key,
            kvp => new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        var stages = new List<ExecutionPlanStage>();
        var stageNumber = 1;

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ready.Count == 0)
            {
                break;
            }

            stages.Add(new ExecutionPlanStage(stageNumber, ready));
            stageNumber++;

            foreach (var stepId in ready)
            {
                remaining.Remove(stepId);
            }

            foreach (var deps in remaining.Values)
            {
                foreach (var readyStep in ready)
                {
                    deps.Remove(readyStep);
                }
            }
        }

        return stages;
    }

    private static bool TryResolveBundleScriptPath(string activityRef, out string scriptPath, out string error)
    {
        scriptPath = string.Empty;
        error = string.Empty;

        if (!TryNormalizeRelativePath(activityRef, out var normalized, out error))
        {
            return false;
        }

        if (normalized.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
        {
            scriptPath = normalized;
        }
        else
        {
            scriptPath = $"scripts/{normalized}";
        }

        return true;
    }

    private static bool TryNormalizeRelativePath(string input, out string normalizedPath, out string error)
    {
        normalizedPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "ActivityRef cannot be empty.";
            return false;
        }

        var candidate = input.Replace('\\', '/').Trim();
        if (candidate.StartsWith('/') || Path.IsPathRooted(candidate))
        {
            error = "Absolute paths are not allowed.";
            return false;
        }

        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            error = "Path is empty.";
            return false;
        }

        if (segments.Any(s => s == "." || s == ".."))
        {
            error = "Path traversal segments are not allowed.";
            return false;
        }

        normalizedPath = string.Join('/', segments);
        return true;
    }

    private static HashSet<string> CollectBundleIds(WorkflowDefinition? definition)
    {
        var bundleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (definition is null)
        {
            return bundleIds;
        }

        foreach (var step in definition.Steps)
        {
            const string prefix = "bundle://";
            if (!step.ActivityRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = step.ActivityRef[prefix.Length..];
            var slashIndex = remainder.IndexOf('/');
            if (slashIndex <= 0)
            {
                continue;
            }

            var bundleId = remainder[..slashIndex];
            if (Guid.TryParse(bundleId, out _))
            {
                bundleIds.Add(bundleId);
            }
        }

        return bundleIds;
    }

    private static void ExtractZipSafely(Stream zipStream, string destinationDirectory, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var destinationRoot = Path.GetFullPath(destinationDirectory);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName.Replace('\\', '/')));
            if (!fullPath.StartsWith(destinationRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Bundle contains invalid path '{entry.FullName}'.");
            }

            if (entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            entry.ExtractToFile(fullPath, overwrite: true);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(destinationDirectory, relative);
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            File.Copy(file, destination, overwrite: true);
        }
    }

    private string GetStorageRootPath()
    {
        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.StorageRoot));
    }

    private string GetPreviewRootPath()
    {
        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.PreviewRoot));
    }

    private static string ToRelativeUnixPath(string root, string fullPath)
    {
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
