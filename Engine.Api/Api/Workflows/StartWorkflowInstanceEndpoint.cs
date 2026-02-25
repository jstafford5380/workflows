using System.Text.Json.Nodes;
using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class StartWorkflowInstanceEndpoint : Endpoint<StartWorkflowInstanceRequest, WorkflowInstanceChecklistResponse>
{
    private readonly IWorkflowEngineService _engine;

    public StartWorkflowInstanceEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("workflows/{workflowName}/instances");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Start a workflow instance";
            s.Description = "Creates a workflow instance and schedules initial runnable steps based on dependencies.";
            s.RequestParam(r => r.WorkflowName, "Workflow name to start.");
            s.RequestParam(r => r.Version, "Optional workflow version. Latest version is used when omitted.");
            s.RequestParam(r => r.Inputs!, "Workflow input object.");
            s.Response<WorkflowInstanceChecklistResponse>(StatusCodes.Status201Created, "Workflow instance created.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Missing route parameter or invalid request payload.");
        });

        Description(b => b
            .Produces<WorkflowInstanceChecklistResponse>(StatusCodes.Status201Created, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(StartWorkflowInstanceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.WorkflowName))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse("Missing route parameter 'workflowName'."),
                cancellationToken: ct);
            return;
        }

        var view = await _engine.StartWorkflowAsync(
            req.WorkflowName,
            req.Inputs ?? new JsonObject(),
            req.Version,
            ct);

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        HttpContext.Response.Headers.Location = $"/instances/{view.InstanceId}";
        await HttpContext.Response.WriteAsJsonAsync(WorkflowInstanceChecklistResponse.FromModel(view), cancellationToken: ct);
    }
}
