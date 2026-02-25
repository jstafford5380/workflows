using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine.Core.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Activities;

public sealed class ScriptActivityRunner
{
    private readonly ActivityRunnerOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<ScriptActivityRunner> _logger;

    public ScriptActivityRunner(
        IOptions<ActivityRunnerOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<ScriptActivityRunner> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public bool CanHandle(string activityRef)
    {
        return _options.ScriptMap.ContainsKey(activityRef)
               || TryParseBundleActivityRef(activityRef, out _, out _);
    }

    public async Task<ActivityExecutionResult> RunAsync(ActivityExecutionRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveScriptPath(request.ActivityRef, out var resolvedScriptPath, out var error))
        {
            return new ActivityExecutionResult(false, new JsonObject(), error, false);
        }

        if (!File.Exists(resolvedScriptPath))
        {
            return new ActivityExecutionResult(
                false,
                new JsonObject(),
                $"Script path '{resolvedScriptPath}' does not exist.",
                false);
        }

        var requestPayload = new JsonObject
        {
            ["instanceId"] = request.InstanceId.ToString(),
            ["stepId"] = request.StepId,
            ["activityRef"] = request.ActivityRef,
            ["idempotencyKey"] = request.IdempotencyKey,
            ["inputs"] = request.Inputs.DeepClone()
        };

        var tempInputPath = Path.Combine(Path.GetTempPath(), $"activity-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempInputPath, requestPayload.ToJsonString(), cancellationToken);

        try
        {
            var startInfo = BuildStartInfo(resolvedScriptPath, tempInputPath, request);
            using var process = new Process { StartInfo = startInfo };

            process.Start();

            var timeoutSeconds = Math.Max(5, _options.DefaultTimeoutSeconds);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            string stdout;
            string stderr;
            try
            {
                stdout = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                stderr = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKillProcess(process);
                return new ActivityExecutionResult(
                    false,
                    new JsonObject(),
                    $"Script activity '{request.ActivityRef}' timed out after {timeoutSeconds} seconds.",
                    true);
            }

            if (process.ExitCode != 0)
            {
                var errorMessage =
                    $"Script '{resolvedScriptPath}' exited with code {process.ExitCode}. stderr: {Truncate(stderr, 2000)} stdout: {Truncate(stdout, 2000)}";
                _logger.LogWarning("{ErrorMessage}", errorMessage);
                return new ActivityExecutionResult(false, new JsonObject(), errorMessage, true);
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return new ActivityExecutionResult(
                    false,
                    new JsonObject(),
                    $"Script '{resolvedScriptPath}' returned empty stdout; expected JSON.",
                    false);
            }

            return ParseScriptOutput(stdout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script activity execution failed for {ActivityRef}", request.ActivityRef);
            return new ActivityExecutionResult(false, new JsonObject(), ex.Message, true);
        }
        finally
        {
            TryDeleteFile(tempInputPath);
        }
    }

    private bool TryResolveScriptPath(string activityRef, out string resolvedScriptPath, out string error)
    {
        resolvedScriptPath = string.Empty;
        error = string.Empty;

        if (_options.ScriptMap.TryGetValue(activityRef, out var configuredScriptPath))
        {
            var mappedPath = ResolveMappedScriptPath(configuredScriptPath);
            if (mappedPath is null)
            {
                error =
                    $"Configured script mapping for '{activityRef}' points outside configured ScriptsBasePath '{_options.ScriptsBasePath}'.";
                return false;
            }

            resolvedScriptPath = mappedPath;
            return true;
        }

        if (TryParseBundleActivityRef(activityRef, out var bundleId, out var relativePath))
        {
            var bundlePath = ResolveBundleScriptPath(bundleId, relativePath);
            if (bundlePath is null)
            {
                error = $"Bundle activityRef '{activityRef}' is invalid or outside bundle storage root.";
                return false;
            }

            resolvedScriptPath = bundlePath;
            return true;
        }

        error = $"No script mapping configured for activityRef '{activityRef}'.";
        return false;
    }

    private ProcessStartInfo BuildStartInfo(string scriptPath, string inputPath, ActivityExecutionRequest request)
    {
        ProcessStartInfo startInfo;

        var extension = Path.GetExtension(scriptPath);
        if (OperatingSystem.IsWindows())
        {
            if (string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                startInfo = new ProcessStartInfo("pwsh");
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(scriptPath);
                startInfo.ArgumentList.Add(inputPath);
            }
            else
            {
                startInfo = new ProcessStartInfo("cmd.exe");
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add($"\"{scriptPath}\" \"{inputPath}\"");
            }
        }
        else
        {
            if (string.Equals(extension, ".sh", StringComparison.OrdinalIgnoreCase))
            {
                startInfo = new ProcessStartInfo("/bin/sh");
                startInfo.ArgumentList.Add(scriptPath);
                startInfo.ArgumentList.Add(inputPath);
            }
            else
            {
                startInfo = new ProcessStartInfo(scriptPath);
                startInfo.ArgumentList.Add(inputPath);
            }
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? _hostEnvironment.ContentRootPath;

        startInfo.Environment["WORKFLOW_INSTANCE_ID"] = request.InstanceId.ToString();
        startInfo.Environment["WORKFLOW_STEP_ID"] = request.StepId;
        startInfo.Environment["WORKFLOW_ACTIVITY_REF"] = request.ActivityRef;
        startInfo.Environment["WORKFLOW_IDEMPOTENCY_KEY"] = request.IdempotencyKey;
        startInfo.Environment["WORKFLOW_INPUTS_JSON"] = request.Inputs.ToJsonString();

        return startInfo;
    }

    private static ActivityExecutionResult ParseScriptOutput(string stdout)
    {
        JsonObject parsed;
        try
        {
            var node = JsonNode.Parse(stdout);
            if (node is not JsonObject obj)
            {
                return new ActivityExecutionResult(false, new JsonObject(), "Script output must be a JSON object.", false);
            }

            parsed = obj;
        }
        catch (JsonException ex)
        {
            return new ActivityExecutionResult(false, new JsonObject(), $"Failed to parse script JSON output: {ex.Message}", false);
        }

        var hasEnvelopeKeys = parsed.ContainsKey("success") || parsed.ContainsKey("outputs") || parsed.ContainsKey("errorMessage") ||
                              parsed.ContainsKey("retryable");
        if (!hasEnvelopeKeys)
        {
            return new ActivityExecutionResult(true, parsed, null, true);
        }

        var success = parsed["success"]?.GetValue<bool>() ?? true;
        var retryable = parsed["retryable"]?.GetValue<bool>() ?? true;
        var errorMessage = parsed["errorMessage"]?.GetValue<string>();
        var outputs = parsed["outputs"] as JsonObject ?? new JsonObject();

        if (!success && string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "Script reported success=false without an error message.";
        }

        return new ActivityExecutionResult(success, outputs, errorMessage, retryable);
    }

    private string? ResolveMappedScriptPath(string configuredPath)
    {
        var scriptsBasePath = Path.GetFullPath(Path.IsPathRooted(_options.ScriptsBasePath)
            ? _options.ScriptsBasePath
            : Path.Combine(_hostEnvironment.ContentRootPath, _options.ScriptsBasePath));

        var fullScriptPath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_hostEnvironment.ContentRootPath, configuredPath));

        if (!fullScriptPath.StartsWith(scriptsBasePath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            return null;
        }

        return fullScriptPath;
    }

    private string? ResolveBundleScriptPath(string bundleId, string relativePath)
    {
        if (!Guid.TryParse(bundleId, out _))
        {
            return null;
        }

        var normalizedPath = NormalizeRelativePath(relativePath);
        if (normalizedPath is null)
        {
            return null;
        }

        var bundleStorageRoot = Path.GetFullPath(Path.IsPathRooted(_options.BundleStoragePath)
            ? _options.BundleStoragePath
            : Path.Combine(_hostEnvironment.ContentRootPath, _options.BundleStoragePath));

        var bundleRoot = Path.Combine(bundleStorageRoot, bundleId);
        var fullScriptPath = Path.GetFullPath(Path.Combine(bundleRoot, normalizedPath));

        if (!fullScriptPath.StartsWith(bundleRoot, RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            return null;
        }

        return fullScriptPath;
    }

    private static bool TryParseBundleActivityRef(string activityRef, out string bundleId, out string relativePath)
    {
        bundleId = string.Empty;
        relativePath = string.Empty;

        const string prefix = "bundle://";
        if (!activityRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = activityRef[prefix.Length..];
        var separatorIndex = remainder.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex >= remainder.Length - 1)
        {
            return false;
        }

        bundleId = remainder[..separatorIndex];
        relativePath = remainder[(separatorIndex + 1)..];
        return true;
    }

    private static string? NormalizeRelativePath(string input)
    {
        var candidate = input.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith('/') || Path.IsPathRooted(candidate))
        {
            return null;
        }

        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s == "." || s == ".."))
        {
            return null;
        }

        return string.Join('/', segments);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + "...";
    }
}
