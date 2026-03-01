using System.Text.Json;
using Engine.Api.Bundles;
using Engine.Core.Definitions;
using Engine.Core.Domain;
using Engine.Runtime.Contracts;
using Microsoft.Extensions.Options;

namespace Engine.Api.Drafts;

public sealed class DraftWorkflowPublisher : IDraftWorkflowPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkflowEngineService _engine;
    private readonly IDraftScriptStore _draftScriptStore;
    private readonly IWebHostEnvironment _environment;
    private readonly BundleOptions _bundleOptions;
    private readonly ILogger<DraftWorkflowPublisher> _logger;

    public DraftWorkflowPublisher(
        IWorkflowEngineService engine,
        IDraftScriptStore draftScriptStore,
        IWebHostEnvironment environment,
        IOptions<BundleOptions> bundleOptions,
        ILogger<DraftWorkflowPublisher> logger)
    {
        _engine = engine;
        _draftScriptStore = draftScriptStore;
        _environment = environment;
        _bundleOptions = bundleOptions.Value;
        _logger = logger;
    }

    public async Task<WorkflowDefinitionMetadata> PublishAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await _engine.GetWorkflowDraftAsync(draftId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow draft '{draftId}' was not found.");

        var scripts = await _draftScriptStore.ListScriptsAsync(draftId, cancellationToken);
        if (scripts.Count == 0)
        {
            return await _engine.PublishWorkflowDraftAsync(draftId, cancellationToken);
        }

        var scriptsByPath = scripts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingDefinition = await _engine.GetWorkflowDefinitionAsync(
            draft.Definition.Name,
            draft.Definition.Version,
            cancellationToken);
        var priorBundleIds = CollectBundleIds(existingDefinition);

        var bundleId = Guid.NewGuid().ToString("N");
        var copiedScripts = await _draftScriptStore.CopyScriptsToBundleAsync(draftId, bundleId, cancellationToken);
        var transformedSteps = draft.Definition.Steps
            .Select(step =>
            {
                if (step.WaitForEvent is not null)
                {
                    return step;
                }

                if (!TryNormalizeDraftScriptPath(step.ActivityRef, out var scriptPath))
                {
                    return step;
                }

                if (!scriptsByPath.Contains(scriptPath))
                {
                    return step;
                }

                return step with { ActivityRef = $"bundle://{bundleId}/{scriptPath}" };
            })
            .ToList();
        var transformedDefinition = draft.Definition with { Steps = transformedSteps };

        var metadata = await _engine.RegisterWorkflowDefinitionAsync(transformedDefinition, cancellationToken);
        await WriteBundleMetadataAsync(bundleId, metadata, copiedScripts, cancellationToken);
        CleanupReplacedBundles(bundleId, priorBundleIds);
        return metadata;
    }

    private async Task WriteBundleMetadataAsync(
        string bundleId,
        WorkflowDefinitionMetadata metadata,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken)
    {
        var bundleStorageRoot = Path.GetFullPath(Path.IsPathRooted(_bundleOptions.StorageRoot)
            ? _bundleOptions.StorageRoot
            : Path.Combine(_environment.ContentRootPath, _bundleOptions.StorageRoot));
        var bundleRoot = Path.Combine(bundleStorageRoot, bundleId);
        Directory.CreateDirectory(bundleRoot);

        var metadataPath = Path.Combine(bundleRoot, "bundle-metadata.json");
        var payload = new
        {
            bundleId,
            bundleFileName = "draft-publish",
            name = metadata.Name,
            version = metadata.Version,
            revision = metadata.Revision,
            registeredAt = metadata.RegisteredAt,
            files
        };
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
    }

    private void CleanupReplacedBundles(string currentBundleId, IReadOnlyCollection<string> priorBundleIds)
    {
        foreach (var priorBundleId in priorBundleIds.Where(x => !string.Equals(x, currentBundleId, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var bundleStorageRoot = Path.GetFullPath(Path.IsPathRooted(_bundleOptions.StorageRoot)
                    ? _bundleOptions.StorageRoot
                    : Path.Combine(_environment.ContentRootPath, _bundleOptions.StorageRoot));
                var priorBundleRoot = Path.Combine(bundleStorageRoot, priorBundleId);
                if (Directory.Exists(priorBundleRoot))
                {
                    Directory.Delete(priorBundleRoot, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean replaced bundle {BundleId}", priorBundleId);
            }
        }
    }

    private static IReadOnlySet<string> CollectBundleIds(WorkflowDefinition? definition)
    {
        if (definition is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in definition.Steps)
        {
            if (step.WaitForEvent is not null)
            {
                continue;
            }

            if (!step.ActivityRef.StartsWith("bundle://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = step.ActivityRef["bundle://".Length..];
            var slashIndex = remainder.IndexOf('/');
            if (slashIndex <= 0)
            {
                continue;
            }

            ids.Add(remainder[..slashIndex]);
        }

        return ids;
    }

    private static bool TryNormalizeDraftScriptPath(string activityRef, out string scriptPath)
    {
        scriptPath = string.Empty;
        if (string.IsNullOrWhiteSpace(activityRef))
        {
            return false;
        }

        if (activityRef.StartsWith("bundle://", StringComparison.OrdinalIgnoreCase)
            || activityRef.StartsWith("local.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = activityRef.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        normalized = normalized.TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        scriptPath = normalized;
        return true;
    }
}
