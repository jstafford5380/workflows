using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Instances;

public sealed class CancelInstanceEndpoint : Endpoint<CancelInstanceRequest>
{
    private readonly IWorkflowEngineService _engine;

    public CancelInstanceEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("instances/{instanceId:guid}/cancel");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Cancel workflow instance";
            s.Description = "Marks a running workflow instance as canceled.";
            s.RequestParam(r => r.InstanceId, "Workflow instance identifier.");
            s.Response(StatusCodes.Status202Accepted, "Cancel request accepted.");
            s.Response(StatusCodes.Status404NotFound, "Instance not found.");
        });

        Description(b => b
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(CancelInstanceRequest req, CancellationToken ct)
    {
        var canceled = await _engine.CancelInstanceAsync(req.InstanceId, ct);

        if (!canceled)
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
