using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Approvals;

public sealed class GetApprovalEndpoint : Endpoint<ApprovalByIdRequest, ApprovalResponse>
{
    private readonly IWorkflowEngineService _engine;

    public GetApprovalEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("approvals/{approvalId:guid}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get approval";
            s.Description = "Returns a single approval request with comment trail.";
            s.Response<ApprovalResponse>(StatusCodes.Status200OK, "Approval request.");
            s.Response(StatusCodes.Status404NotFound, "Approval not found.");
        });
    }

    public override async Task HandleAsync(ApprovalByIdRequest req, CancellationToken ct)
    {
        var approval = await _engine.GetApprovalAsync(req.ApprovalId, ct);
        if (approval is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.CompleteAsync();
            return;
        }

        await HttpContext.Response.WriteAsJsonAsync(ApprovalResponse.FromModel(approval), cancellationToken: ct);
    }
}
