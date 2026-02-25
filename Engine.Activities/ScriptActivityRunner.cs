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
        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"activity-output-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempInputPath, requestPayload.ToJsonString(), cancellationToken);
        await File.WriteAllTextAsync(tempOutputPath, string.Empty, cancellationToken);

        if (!TryBuildOrderedArguments(request, out var orderedArguments, out var parameterError))
        {
            return new ActivityExecutionResult(false, new JsonObject(), parameterError, false);
        }

        try
        {
            var startInfo = BuildStartInfo(resolvedScriptPath, tempInputPath, tempOutputPath, request, orderedArguments);
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
                var consoleOutput = BuildConsoleOutput(stdout, stderr);
                var errorMessage =
                    $"Script '{resolvedScriptPath}' exited with code {process.ExitCode}. stderr: {Truncate(stderr, 2000)} stdout: {Truncate(stdout, 2000)}";
                _logger.LogWarning("{ErrorMessage}", errorMessage);
                return new ActivityExecutionResult(false, new JsonObject(), errorMessage, true, consoleOutput);
            }

            var successfulConsoleOutput = BuildConsoleOutput(stdout, stderr);
            var outputResult = await TryParseOutputFileAsync(tempOutputPath, cancellationToken);
            if (outputResult is not null)
            {
                return outputResult with { ConsoleOutput = successfulConsoleOutput };
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return new ActivityExecutionResult(true, new JsonObject(), null, true, successfulConsoleOutput);
            }

            return ParseScriptOutput(stdout) with { ConsoleOutput = successfulConsoleOutput };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script activity execution failed for {ActivityRef}", request.ActivityRef);
            return new ActivityExecutionResult(false, new JsonObject(), ex.Message, true);
        }
        finally
        {
            TryDeleteFile(tempInputPath);
            TryDeleteFile(tempOutputPath);
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

    private ProcessStartInfo BuildStartInfo(
        string scriptPath,
        string inputPath,
        string outputPath,
        ActivityExecutionRequest request,
        IReadOnlyList<string> orderedArguments)
    {
        ProcessStartInfo startInfo;
        var useOrderedArguments = orderedArguments.Count > 0;

        var extension = Path.GetExtension(scriptPath);
        if (OperatingSystem.IsWindows())
        {
            if (string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                startInfo = new ProcessStartInfo("pwsh");
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(scriptPath);
                foreach (var argument in useOrderedArguments ? orderedArguments : new[] { inputPath })
                {
                    startInfo.ArgumentList.Add(argument);
                }
            }
            else
            {
                startInfo = new ProcessStartInfo("cmd.exe");
                startInfo.ArgumentList.Add("/c");
                var commandParts = new List<string> { QuoteForCommand(scriptPath) };
                var args = useOrderedArguments ? orderedArguments : new[] { inputPath };
                commandParts.AddRange(args.Select(QuoteForCommand));
                startInfo.ArgumentList.Add(string.Join(' ', commandParts));
            }
        }
        else
        {
            if (string.Equals(extension, ".sh", StringComparison.OrdinalIgnoreCase))
            {
                startInfo = new ProcessStartInfo("/bin/sh");
                startInfo.ArgumentList.Add(scriptPath);
                foreach (var argument in useOrderedArguments ? orderedArguments : new[] { inputPath })
                {
                    startInfo.ArgumentList.Add(argument);
                }
            }
            else
            {
                startInfo = new ProcessStartInfo(scriptPath);
                foreach (var argument in useOrderedArguments ? orderedArguments : new[] { inputPath })
                {
                    startInfo.ArgumentList.Add(argument);
                }
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
        startInfo.Environment["WORKFLOW_INPUT_PATH"] = inputPath;
        startInfo.Environment["OZ_OUTPUT"] = outputPath;

        return startInfo;
    }

    private static bool TryBuildOrderedArguments(
        ActivityExecutionRequest request,
        out IReadOnlyList<string> orderedArguments,
        out string error)
    {
        orderedArguments = [];
        error = string.Empty;

        if (request.ScriptParameters.Count == 0)
        {
            return true;
        }

        var values = new List<string>(request.ScriptParameters.Count);
        foreach (var parameter in request.ScriptParameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                error = $"Script parameter name is required for step '{request.StepId}'.";
                return false;
            }

            request.Inputs.TryGetPropertyValue(parameter.Name, out var rawValue);
            if (rawValue is null)
            {
                if (parameter.Required)
                {
                    error =
                        $"Required script parameter '{parameter.Name}' is missing for step '{request.StepId}'. Ensure workflow inputs and bindings provide this value.";
                    return false;
                }

                values.Add(string.Empty);
                continue;
            }

            values.Add(ConvertToArgument(rawValue));
        }

        orderedArguments = values;
        return true;
    }

    private static string ConvertToArgument(JsonNode value)
    {
        if (value is JsonValue jsonValue)
        {
            try
            {
                return jsonValue.GetValue<string>();
            }
            catch (InvalidOperationException)
            {
            }
            catch (FormatException)
            {
            }
        }

        return value.ToJsonString();
    }

    private static string QuoteForCommand(string value)
    {
        var escaped = value.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static async Task<ActivityExecutionResult?> TryParseOutputFileAsync(string outputPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(outputPath))
        {
            return null;
        }

        var rawText = await File.ReadAllTextAsync(outputPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var parseResult = TryParseOutputLines(rawText);
        if (!parseResult.IsSuccess)
        {
            return new ActivityExecutionResult(false, new JsonObject(), parseResult.Error, false);
        }

        return new ActivityExecutionResult(true, parseResult.Outputs!, null, true);
    }

    private static (bool IsSuccess, JsonObject? Outputs, string? Error) TryParseOutputLines(string rawText)
    {
        var outputs = new JsonObject();
        var lines = rawText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var heredocIndex = line.IndexOf("<<", StringComparison.Ordinal);
            if (heredocIndex > 0)
            {
                var name = line[..heredocIndex].Trim();
                var marker = line[(heredocIndex + 2)..];
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(marker))
                {
                    return (false, null, $"Invalid OZ_OUTPUT line '{line}'. Expected name<<MARKER.");
                }

                var valueLines = new List<string>();
                var foundMarker = false;
                for (index += 1; index < lines.Length; index++)
                {
                    if (string.Equals(lines[index], marker, StringComparison.Ordinal))
                    {
                        foundMarker = true;
                        break;
                    }

                    valueLines.Add(lines[index]);
                }

                if (!foundMarker)
                {
                    return (false, null, $"Missing closing marker '{marker}' for OZ_OUTPUT key '{name}'.");
                }

                outputs[name] = CoerceOutputValue(string.Join('\n', valueLines));
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                return (false, null, $"Invalid OZ_OUTPUT line '{line}'. Expected key=value.");
            }

            var key = line[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return (false, null, $"Invalid OZ_OUTPUT line '{line}'. Missing key.");
            }

            var value = line[(separatorIndex + 1)..];
            outputs[key] = CoerceOutputValue(value);
        }

        return (true, outputs, null);
    }

    private static JsonNode? CoerceOutputValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return JsonNode.Parse(trimmed);
        }
        catch (JsonException)
        {
            return value;
        }
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

    private static string BuildConsoleOutput(string stdout, string stderr)
    {
        return $"--- stdout ---{Environment.NewLine}{stdout}{Environment.NewLine}--- stderr ---{Environment.NewLine}{stderr}";
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
