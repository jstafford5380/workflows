using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Instances;

public sealed class RetryStepEndpoint : Endpoint<RetryStepRequest>
{
    private readonly IWorkflowEngineService _engine;

    public RetryStepEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("instances/{instanceId:guid}/steps/{stepId}/retry");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Retry a workflow step";
            s.Description = "Queues retry execution for a specific step in an instance.";
            s.RequestParam(r => r.InstanceId, "Workflow instance identifier.");
            s.RequestParam(r => r.StepId, "Step identifier.");
            s.Response(StatusCodes.Status202Accepted, "Retry request accepted.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Step id route parameter is missing.");
            s.Response(StatusCodes.Status404NotFound, "Step or instance not found.");
        });

        Description(b => b
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json")
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(RetryStepRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.StepId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse("Missing route parameter 'stepId'."),
                cancellationToken: ct);
            return;
        }

        var queued = await _engine.RetryStepAsync(req.InstanceId, req.StepId, ct);

        if (!queued)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.CompleteAsync();
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status202Accepted;
        HttpContext.Response.Headers.Location = $"/instances/{req.InstanceId}";
        await HttpContext.Response.CompleteAsync();
    }
}
