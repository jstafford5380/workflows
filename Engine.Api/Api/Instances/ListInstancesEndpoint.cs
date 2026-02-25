using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Instances;

public sealed record WorkflowInstanceSummaryResponse(
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    Engine.Core.Domain.WorkflowInstanceStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class ListInstancesEndpoint : EndpointWithoutRequest<IReadOnlyList<WorkflowInstanceSummaryResponse>>
{
    private readonly IWorkflowEngineService _engine;

    public ListInstancesEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("instances");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List workflow instances";
            s.Description = "Returns recent workflow runs for browsing and drill-in.";
            s.Response<IReadOnlyList<WorkflowInstanceSummaryResponse>>(StatusCodes.Status200OK, "Workflow run summaries.");
        });

        Description(b => b.Produces<IReadOnlyList<WorkflowInstanceSummaryResponse>>(StatusCodes.Status200OK, "application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var instances = await _engine.ListInstancesAsync(ct);
        var response = instances
            .Select(x => new WorkflowInstanceSummaryResponse(
                x.InstanceId,
                x.WorkflowName,
                x.WorkflowVersion,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();

        await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
    }
}
