using Engine.Api.Drafts;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class ListWorkflowDraftScriptsEndpoint : Endpoint<WorkflowDraftByIdRequest, IReadOnlyList<DraftScriptResponse>>
{
    private readonly IWorkflowEngineService _engine;
    private readonly IDraftScriptStore _draftScriptStore;

    public ListWorkflowDraftScriptsEndpoint(IWorkflowEngineService engine, IDraftScriptStore draftScriptStore)
    {
        _engine = engine;
        _draftScriptStore = draftScriptStore;
    }

    public override void Configure()
    {
        Get("workflow-drafts/{draftId:guid}/scripts");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List workflow draft scripts";
            s.Description = "Returns uploaded scripts for a workflow draft.";
            s.Response<IReadOnlyList<DraftScriptResponse>>(StatusCodes.Status200OK, "Draft scripts.");
            s.Response(StatusCodes.Status404NotFound, "Draft not found.");
        });
    }

    public override async Task HandleAsync(WorkflowDraftByIdRequest req, CancellationToken ct)
    {
        var draft = await _engine.GetWorkflowDraftAsync(req.DraftId, ct);
        if (draft is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var scripts = await _draftScriptStore.ListScriptsAsync(req.DraftId, ct);
        var response = scripts.Select(path => new DraftScriptResponse(path)).ToList();
        await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
    }
}
