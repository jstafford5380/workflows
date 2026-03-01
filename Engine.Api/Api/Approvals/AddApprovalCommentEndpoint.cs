using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Approvals;

public sealed class AddApprovalCommentEndpoint : Endpoint<ApprovalCommentRequest, ApprovalResponse>
{
    private readonly IWorkflowEngineService _engine;

    public AddApprovalCommentEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("approvals/{approvalId:guid}/comments");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Add an approval comment";
            s.Description = "Appends a comment to an approval request comment trail without changing approval status.";
            s.Response<ApprovalResponse>(StatusCodes.Status200OK, "Updated approval request.");
            s.Response(StatusCodes.Status400BadRequest, "Comment is required.");
            s.Response(StatusCodes.Status404NotFound, "Approval not found.");
        });
    }

    public override async Task HandleAsync(ApprovalCommentRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Comment))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse("Comment is required."), cancellationToken: ct);
            return;
        }

        var actor = string.IsNullOrWhiteSpace(req.Actor) ? "manual" : req.Actor.Trim();
        var updated = await _engine.AddApprovalCommentAsync(req.ApprovalId, actor, req.Comment, ct);
        if (updated is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse("Approval not found."), cancellationToken: ct);
            return;
        }

        await HttpContext.Response.WriteAsJsonAsync(ApprovalResponse.FromModel(updated), cancellationToken: ct);
    }
}
