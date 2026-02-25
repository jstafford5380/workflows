using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Instances;

public sealed record StepExecutionLogResponse(
    int Attempt,
    bool IsSuccess,
    string ConsoleOutput,
    DateTimeOffset CreatedAt);

public sealed class GetStepLogsEndpoint : Endpoint<GetStepLogsRequest, IReadOnlyList<StepExecutionLogResponse>>
{
    private readonly IWorkflowEngineService _engine;

    public GetStepLogsEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("instances/{instanceId:guid}/steps/{stepId}/logs");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get step execution logs";
            s.Description = "Returns captured console output for each recorded execution attempt of a step.";
            s.RequestParam(r => r.InstanceId, "Workflow instance identifier.");
            s.RequestParam(r => r.StepId, "Step identifier.");
            s.Response<IReadOnlyList<StepExecutionLogResponse>>(StatusCodes.Status200OK, "Step execution logs.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Missing step id route parameter.");
        });

        Description(b => b
            .Produces<IReadOnlyList<StepExecutionLogResponse>>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(GetStepLogsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.StepId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse("Missing route parameter 'stepId'."),
                cancellationToken: ct);
            return;
        }

        var logs = await _engine.GetStepExecutionLogsAsync(req.InstanceId, req.StepId, ct);
        var response = logs
            .Select(x => new StepExecutionLogResponse(
                x.Attempt,
                x.IsSuccess,
                x.ConsoleOutput,
                x.CreatedAt))
            .ToList();

        await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
    }
}
