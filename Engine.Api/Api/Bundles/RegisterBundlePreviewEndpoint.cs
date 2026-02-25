using Engine.Api.Api.Common;
using Engine.Api.Bundles;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Bundles;

public sealed class RegisterBundlePreviewEndpoint : Endpoint<RegisterBundlePreviewRequest, BundleRegisterApiResponse>
{
    private readonly IBundleService _bundleService;
    private readonly IWorkflowEngineService _workflowEngine;

    public RegisterBundlePreviewEndpoint(IBundleService bundleService, IWorkflowEngineService workflowEngine)
    {
        _bundleService = bundleService;
        _workflowEngine = workflowEngine;
    }

    public override void Configure()
    {
        Post("bundles/previews/{previewId}/register");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Register a bundle preview";
            s.Description = "Persists a validated bundle and registers the transformed workflow definition.";
            s.RequestParam(r => r.PreviewId, "Preview identifier to register.");
            s.Response<BundleRegisterApiResponse>(StatusCodes.Status200OK, "Bundle registered successfully.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Preview is invalid, missing, or cannot be registered.");
        });

        Description(b => b
            .Produces<BundleRegisterApiResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(RegisterBundlePreviewRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PreviewId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse("Missing route parameter 'previewId'."),
                cancellationToken: ct);
            return;
        }

        try
        {
            var registered = await _bundleService.RegisterPreviewAsync(req.PreviewId, _workflowEngine, ct);
            await HttpContext.Response.WriteAsJsonAsync(BundleRegisterApiResponse.FromModel(registered), cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(ex.Message), cancellationToken: ct);
        }
    }
}
