using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class RegisterWorkflowEndpoint : Endpoint<RegisterWorkflowRequest, WorkflowVersionResponse>
{
    private readonly IWorkflowEngineService _engine;

    public RegisterWorkflowEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("workflows/register");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Register a workflow definition";
            s.Description = "Registers a versioned workflow definition after validation by the workflow engine.";
            s.RequestParam(r => r.Name, "Workflow name.");
            s.RequestParam(r => r.Version, "Workflow version.");
            s.Response<WorkflowVersionResponse>(StatusCodes.Status202Accepted, "Workflow registered and accepted.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Workflow definition is invalid.");
        });

        Description(b => b
            .Accepts<RegisterWorkflowRequest>("application/json")
            .Produces<WorkflowVersionResponse>(StatusCodes.Status202Accepted, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(RegisterWorkflowRequest req, CancellationToken ct)
    {
        try
        {
            var definition = req.ToDefinition();
            var metadata = await _engine.RegisterWorkflowDefinitionAsync(definition, ct);

            HttpContext.Response.StatusCode = StatusCodes.Status202Accepted;
            HttpContext.Response.Headers.Location = $"/workflows/{definition.Name}";
            await HttpContext.Response.WriteAsJsonAsync(
                new WorkflowVersionResponse(metadata.Name, metadata.Version, metadata.Revision),
                cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(ex.Message), cancellationToken: ct);
        }
    }
}
