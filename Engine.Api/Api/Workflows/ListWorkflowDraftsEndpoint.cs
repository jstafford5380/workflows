using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class ListWorkflowDraftsEndpoint : EndpointWithoutRequest<IReadOnlyList<WorkflowDraftSummaryResponse>>
{
    private readonly IWorkflowEngineService _engine;

    public ListWorkflowDraftsEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("workflow-drafts");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List workflow drafts";
            s.Description = "Returns saved workflow drafts for recipe authoring.";
            s.Response<IReadOnlyList<WorkflowDraftSummaryResponse>>(StatusCodes.Status200OK, "Workflow draft list.");
        });

        Description(b => b.Produces<IReadOnlyList<WorkflowDraftSummaryResponse>>(StatusCodes.Status200OK, "application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var drafts = await _engine.ListWorkflowDraftsAsync(ct);
        await HttpContext.Response.WriteAsJsonAsync(drafts.Select(WorkflowDraftSummaryResponse.FromModel).ToList(), cancellationToken: ct);
    }
}
