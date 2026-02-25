using Engine.Api.Api.Common;
using Engine.Api.Bundles;
using FastEndpoints;

namespace Engine.Api.Api.Bundles;

public sealed class GetBundlePreviewEndpoint : Endpoint<BundlePreviewByIdRequest, BundlePreviewApiResponse>
{
    private readonly IBundleService _bundleService;

    public GetBundlePreviewEndpoint(IBundleService bundleService)
    {
        _bundleService = bundleService;
    }

    public override void Configure()
    {
        Get("bundles/previews/{previewId}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get bundle preview";
            s.Description = "Returns the preview metadata for a previously uploaded workflow bundle.";
            s.RequestParam(r => r.PreviewId, "Preview identifier returned by the preview endpoint.");
            s.Response<BundlePreviewApiResponse>(StatusCodes.Status200OK, "Bundle preview metadata.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Route parameter is missing.");
            s.Response(StatusCodes.Status404NotFound, "Preview not found or expired.");
        });

        Description(b => b
            .Produces<BundlePreviewApiResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json")
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(BundlePreviewByIdRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PreviewId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse("Missing route parameter 'previewId'."),
                cancellationToken: ct);
            return;
        }

        var preview = await _bundleService.GetPreviewAsync(req.PreviewId, ct);

        if (preview is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.CompleteAsync();
            return;
        }

        await HttpContext.Response.WriteAsJsonAsync(BundlePreviewApiResponse.FromModel(preview), cancellationToken: ct);
    }
}
