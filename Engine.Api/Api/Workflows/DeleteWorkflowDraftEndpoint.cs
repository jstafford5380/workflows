using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class DeleteWorkflowDraftEndpoint : Endpoint<WorkflowDraftByIdRequest>
{
    private readonly IWorkflowEngineService _engine;

    public DeleteWorkflowDraftEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Delete("workflow-drafts/{draftId:guid}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Delete workflow draft";
            s.Description = "Deletes a workflow draft.";
            s.Response(StatusCodes.Status204NoContent, "Draft deleted.");
            s.Response(StatusCodes.Status404NotFound, "Draft not found.");
        });

        Description(b => b
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(WorkflowDraftByIdRequest req, CancellationToken ct)
    {
        var deleted = await _engine.DeleteWorkflowDraftAsync(req.DraftId, ct);
        if (!deleted)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
