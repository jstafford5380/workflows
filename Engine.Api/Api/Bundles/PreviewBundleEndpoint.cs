using Engine.Api.Api.Common;
using Engine.Api.Bundles;
using FastEndpoints;

namespace Engine.Api.Api.Bundles;

public sealed class PreviewBundleEndpoint : Endpoint<PreviewBundleRequest, BundlePreviewApiResponse>
{
    private readonly IBundleService _bundleService;

    public PreviewBundleEndpoint(IBundleService bundleService)
    {
        _bundleService = bundleService;
    }

    public override void Configure()
    {
        Post("bundles/preview");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Preview a workflow bundle";
            s.Description = "Uploads a bundle zip, unpacks and validates it, and returns workflow/script preview metadata.";
            s.RequestParam(r => r.Bundle!, "Bundle zip file field named 'bundle'.");
            s.Response<BundlePreviewApiResponse>(StatusCodes.Status200OK, "Bundle preview generated successfully.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Bundle is invalid or request format is incorrect.");
        });

        Description(b => b
            .Accepts<PreviewBundleRequest>("multipart/form-data")
            .Produces<BundlePreviewApiResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(PreviewBundleRequest req, CancellationToken ct)
    {
        IFormFile? bundleFile = req.Bundle;
        if (bundleFile is null && HttpContext.Request.HasFormContentType)
        {
            var form = await HttpContext.Request.ReadFormAsync(ct);
            bundleFile = form.Files["bundle"] ?? form.Files.FirstOrDefault();
        }

        if (bundleFile is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse("Expected multipart/form-data with form file field named 'bundle'."),
                cancellationToken: ct);
            return;
        }

        try
        {
            var preview = await _bundleService.CreatePreviewAsync(bundleFile, ct);
            await HttpContext.Response.WriteAsJsonAsync(BundlePreviewApiResponse.FromModel(preview), cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(ex.Message), cancellationToken: ct);
        }
    }
}
