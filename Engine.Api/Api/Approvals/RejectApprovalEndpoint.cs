using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Approvals;

public sealed class RejectApprovalEndpoint : Endpoint<ApprovalDecisionRequest, ApprovalResponse>
{
    private readonly IWorkflowEngineService _engine;

    public RejectApprovalEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("approvals/{approvalId:guid}/reject");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Reject an approval request";
            s.Description = "Rejects a waiting approval request and resumes the waiting workflow step with a rejection payload.";
            s.Response<ApprovalResponse>(StatusCodes.Status200OK, "Updated approval request.");
            s.Response(StatusCodes.Status404NotFound, "Approval not found.");
        });
    }

    public override async Task HandleAsync(ApprovalDecisionRequest req, CancellationToken ct)
    {
        var updated = await _engine.ResolveApprovalAsync(req.ApprovalId, false, req.Actor, req.Comment, ct);
        if (updated is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse("Approval not found."), cancellationToken: ct);
            return;
        }

        await HttpContext.Response.WriteAsJsonAsync(ApprovalResponse.FromModel(updated), cancellationToken: ct);
    }
}
