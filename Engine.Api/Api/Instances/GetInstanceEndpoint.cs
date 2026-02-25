using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Instances;

public sealed class GetInstanceEndpoint : Endpoint<GetInstanceRequest, WorkflowInstanceChecklistResponse>
{
    private readonly IWorkflowEngineService _engine;

    public GetInstanceEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("instances/{instanceId:guid}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get instance checklist";
            s.Description = "Returns workflow instance state and checklist-friendly step details.";
            s.RequestParam(r => r.InstanceId, "Workflow instance identifier.");
            s.Response<WorkflowInstanceChecklistResponse>(StatusCodes.Status200OK, "Instance checklist payload.");
            s.Response(StatusCodes.Status404NotFound, "Instance not found.");
        });

        Description(b => b
            .Produces<WorkflowInstanceChecklistResponse>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(GetInstanceRequest req, CancellationToken ct)
    {
        var view = await _engine.GetInstanceChecklistAsync(req.InstanceId, ct);

        if (view is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.CompleteAsync();
            return;
        }

        await HttpContext.Response.WriteAsJsonAsync(WorkflowInstanceChecklistResponse.FromModel(view), cancellationToken: ct);
    }
}
