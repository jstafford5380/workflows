using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Approvals;

public sealed class ListAuditEventsEndpoint : Endpoint<ListAuditEventsRequest, IReadOnlyList<AuditEventResponse>>
{
    private readonly IWorkflowEngineService _engine;

    public ListAuditEventsEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("audit");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List audit events";
            s.Description = "Returns most recent audit trail events across publish/run/approval actions.";
            s.RequestParam(r => r.Take!, "Optional max items (default 200).");
            s.RequestParam(r => r.InstanceId!, "Optional workflow instance id filter.");
            s.RequestParam(r => r.WorkflowName!, "Optional workflow name filter.");
            s.RequestParam(r => r.Category!, "Optional category filter (for example: run, workflow, approval).");
            s.RequestParam(r => r.Action!, "Optional action filter.");
            s.RequestParam(r => r.Actor!, "Optional actor filter.");
            s.RequestParam(r => r.CreatedAfter!, "Optional lower-bound created timestamp filter (ISO 8601).");
            s.RequestParam(r => r.CreatedBefore!, "Optional upper-bound created timestamp filter (ISO 8601).");
            s.Response<IReadOnlyList<AuditEventResponse>>(StatusCodes.Status200OK, "Audit events.");
        });
    }

    public override async Task HandleAsync(ListAuditEventsRequest req, CancellationToken ct)
    {
        var take = req.Take.GetValueOrDefault(200);
        var events = await _engine.ListAuditEventsAsync(
            take,
            req.InstanceId,
            req.WorkflowName,
            req.Category,
            req.Action,
            req.Actor,
            req.CreatedAfter,
            req.CreatedBefore,
            ct);
        await HttpContext.Response.WriteAsJsonAsync(events.Select(AuditEventResponse.FromModel).ToList(), cancellationToken: ct);
    }
}
