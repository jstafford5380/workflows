using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class ListWorkflowsEndpoint : EndpointWithoutRequest<IReadOnlyList<WorkflowDefinitionMetadataResponse>>
{
    private readonly IWorkflowEngineService _engine;

    public ListWorkflowsEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("workflows");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List workflow definitions";
            s.Description = "Returns registered workflow definitions with version and registration timestamp.";
            s.Response<IReadOnlyList<WorkflowDefinitionMetadataResponse>>(StatusCodes.Status200OK, "List of workflow definitions.");
        });

        Description(b => b.Produces<IReadOnlyList<WorkflowDefinitionMetadataResponse>>(StatusCodes.Status200OK, "application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var definitions = await _engine.ListWorkflowDefinitionsAsync(ct);
        var response = definitions.Select(WorkflowDefinitionMetadataResponse.FromModel).ToList();
        await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
    }
}
