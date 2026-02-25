using Engine.Runtime.Contracts;
using Microsoft.AspNetCore.Http;

namespace Engine.Api.Bundles;

public interface IBundleService
{
    Task<BundlePreviewResponse> CreatePreviewAsync(IFormFile bundleFile, CancellationToken cancellationToken);

    Task<BundlePreviewResponse?> GetPreviewAsync(string previewId, CancellationToken cancellationToken);

    Task<BundleRegisterResponse> RegisterPreviewAsync(
        string previewId,
        IWorkflowEngineService workflowEngine,
        CancellationToken cancellationToken);
}
