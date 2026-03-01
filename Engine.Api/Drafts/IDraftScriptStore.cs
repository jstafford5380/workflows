namespace Engine.Api.Drafts;

public interface IDraftScriptStore
{
    Task<IReadOnlyList<string>> ListScriptsAsync(Guid draftId, CancellationToken cancellationToken);

    Task<string> SaveScriptAsync(
        Guid draftId,
        string fileName,
        Stream content,
        string? scriptPath,
        CancellationToken cancellationToken);

    Task<bool> DeleteScriptAsync(Guid draftId, string scriptPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> CopyScriptsToBundleAsync(Guid draftId, string bundleId, CancellationToken cancellationToken);

    Task DeleteDraftScriptsAsync(Guid draftId, CancellationToken cancellationToken);
}
