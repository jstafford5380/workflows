using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Approvals;

public sealed class ListApprovalsEndpoint : Endpoint<ListApprovalsRequest, IReadOnlyList<ApprovalResponse>>
{
    private readonly IWorkflowEngineService _engine;

    public ListApprovalsEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("approvals");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List approval requests";
            s.Description = "Returns approval inbox items for waiting and resolved approvals.";
            s.RequestParam(r => r.Status!, "Optional status filter: waiting, approved, rejected, expired, canceled.");
            s.RequestParam(r => r.InstanceId!, "Optional workflow instance id filter.");
            s.RequestParam(r => r.WorkflowName!, "Optional workflow name filter.");
            s.RequestParam(r => r.Assignee!, "Optional assignee filter.");
            s.RequestParam(r => r.StepId!, "Optional step id filter.");
            s.RequestParam(r => r.CreatedAfter!, "Optional lower-bound created timestamp filter (ISO 8601).");
            s.RequestParam(r => r.CreatedBefore!, "Optional upper-bound created timestamp filter (ISO 8601).");
            s.Response<IReadOnlyList<ApprovalResponse>>(StatusCodes.Status200OK, "Approval requests.");
        });
    }

    public override async Task HandleAsync(ListApprovalsRequest req, CancellationToken ct)
    {
        Engine.Core.Domain.ApprovalRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(req.Status)
            && Enum.TryParse<Engine.Core.Domain.ApprovalRequestStatus>(req.Status, true, out var parsed))
        {
            status = parsed;
        }

        var approvals = await _engine.ListApprovalsAsync(
            status,
            req.InstanceId,
            req.WorkflowName,
            req.Assignee,
            req.StepId,
            req.CreatedAfter,
            req.CreatedBefore,
            ct);
        await HttpContext.Response.WriteAsJsonAsync(approvals.Select(ApprovalResponse.FromModel).ToList(), cancellationToken: ct);
    }
}
