using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Approvals;

public sealed class UpdateApprovalMetadataEndpoint : Endpoint<UpdateApprovalMetadataRequest, ApprovalResponse>
{
    private readonly IWorkflowEngineService _engine;

    public UpdateApprovalMetadataEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Patch("approvals/{approvalId:guid}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Update approval metadata";
            s.Description = "Updates assignee, reason, SLA expiry, and optional comment.";
            s.Response<ApprovalResponse>(StatusCodes.Status200OK, "Updated approval request.");
            s.Response(StatusCodes.Status404NotFound, "Approval not found.");
        });
    }

    public override async Task HandleAsync(UpdateApprovalMetadataRequest req, CancellationToken ct)
    {
        var updated = await _engine.UpdateApprovalMetadataAsync(
            req.ApprovalId,
            req.Assignee,
            req.Reason,
            req.ExpiresAt,
            req.Actor,
            req.Comment,
            ct);

        if (updated is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse("Approval not found."), cancellationToken: ct);
            return;
        }

        await HttpContext.Response.WriteAsJsonAsync(ApprovalResponse.FromModel(updated), cancellationToken: ct);
    }
}
