using System.Runtime.InteropServices;
using Engine.Api.Bundles;
using Microsoft.Extensions.Options;

namespace Engine.Api.Drafts;

public sealed class DraftScriptStore : IDraftScriptStore
{
    private readonly IWebHostEnvironment _environment;
    private readonly BundleOptions _bundleOptions;

    public DraftScriptStore(IWebHostEnvironment environment, IOptions<BundleOptions> bundleOptions)
    {
        _environment = environment;
        _bundleOptions = bundleOptions.Value;
    }

    public Task<IReadOnlyList<string>> ListScriptsAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var root = GetDraftRoot(draftId);
        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => ToRelativeUnixPath(root, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task<string> SaveScriptAsync(
        Guid draftId,
        string fileName,
        Stream content,
        string? scriptPath,
        CancellationToken cancellationToken)
    {
        var relativePath = NormalizeScriptPath(string.IsNullOrWhiteSpace(scriptPath) ? $"scripts/{fileName}" : scriptPath!);
        var root = GetDraftRoot(draftId);
        Directory.CreateDirectory(root);

        var destination = ResolveUnderRoot(root, relativePath);
        if (destination is null)
        {
            throw new InvalidOperationException("Script path is invalid or outside draft script root.");
        }

        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = File.Create(destination);
        await content.CopyToAsync(fileStream, cancellationToken);
        return relativePath;
    }

    public Task<bool> DeleteScriptAsync(Guid draftId, string scriptPath, CancellationToken cancellationToken)
    {
        var normalized = NormalizeScriptPath(scriptPath);
        var root = GetDraftRoot(draftId);
        var target = ResolveUnderRoot(root, normalized);
        if (target is null || !File.Exists(target))
        {
            return Task.FromResult(false);
        }

        File.Delete(target);
        return Task.FromResult(true);
    }

    public async Task<IReadOnlyList<string>> CopyScriptsToBundleAsync(Guid draftId, string bundleId, CancellationToken cancellationToken)
    {
        var draftRoot = GetDraftRoot(draftId);
        if (!Directory.Exists(draftRoot))
        {
            return [];
        }

        var bundleStorageRoot = Path.GetFullPath(Path.IsPathRooted(_bundleOptions.StorageRoot)
            ? _bundleOptions.StorageRoot
            : Path.Combine(_environment.ContentRootPath, _bundleOptions.StorageRoot));
        var bundleRoot = Path.Combine(bundleStorageRoot, bundleId);
        Directory.CreateDirectory(bundleRoot);

        var scripts = await ListScriptsAsync(draftId, cancellationToken);
        foreach (var relativePath in scripts)
        {
            var source = ResolveUnderRoot(draftRoot, relativePath);
            var destination = ResolveUnderRoot(bundleRoot, relativePath);
            if (source is null || destination is null || !File.Exists(source))
            {
                continue;
            }

            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            await using var sourceStream = File.OpenRead(source);
            await using var destinationStream = File.Create(destination);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }

        return scripts;
    }

    public Task DeleteDraftScriptsAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var root = GetDraftRoot(draftId);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    private string GetDraftRoot(Guid draftId)
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "DraftScripts", draftId.ToString("D"));
    }

    private static string NormalizeScriptPath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        normalized = normalized.TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Script path cannot be empty.");
        }

        return normalized;
    }

    private static string? ResolveUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(fullRoot, comparison) ? fullPath : null;
    }

    private static string ToRelativeUnixPath(string root, string fullPath)
    {
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }
}
