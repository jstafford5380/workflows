using Engine.Api.Drafts;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class DeleteWorkflowDraftScriptEndpoint : Endpoint<DraftScriptByPathRequest>
{
    private readonly IWorkflowEngineService _engine;
    private readonly IDraftScriptStore _draftScriptStore;

    public DeleteWorkflowDraftScriptEndpoint(IWorkflowEngineService engine, IDraftScriptStore draftScriptStore)
    {
        _engine = engine;
        _draftScriptStore = draftScriptStore;
    }

    public override void Configure()
    {
        Delete("workflow-drafts/{draftId:guid}/scripts/{**scriptPath}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Delete workflow draft script";
            s.Description = "Deletes one uploaded script from a workflow draft.";
            s.Response(StatusCodes.Status204NoContent, "Script deleted.");
            s.Response(StatusCodes.Status404NotFound, "Draft or script not found.");
        });
    }

    public override async Task HandleAsync(DraftScriptByPathRequest req, CancellationToken ct)
    {
        var draft = await _engine.GetWorkflowDraftAsync(req.DraftId, ct);
        if (draft is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var deleted = await _draftScriptStore.DeleteScriptAsync(req.DraftId, req.ScriptPath, ct);
        HttpContext.Response.StatusCode = deleted ? StatusCodes.Status204NoContent : StatusCodes.Status404NotFound;
    }
}
