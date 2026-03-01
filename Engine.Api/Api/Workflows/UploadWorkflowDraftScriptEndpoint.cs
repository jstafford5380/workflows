using Engine.Api.Api.Common;
using Engine.Api.Drafts;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class UploadWorkflowDraftScriptEndpoint : Endpoint<UploadDraftScriptRequest, DraftScriptResponse>
{
    private readonly IWorkflowEngineService _engine;
    private readonly IDraftScriptStore _draftScriptStore;

    public UploadWorkflowDraftScriptEndpoint(IWorkflowEngineService engine, IDraftScriptStore draftScriptStore)
    {
        _engine = engine;
        _draftScriptStore = draftScriptStore;
    }

    public override void Configure()
    {
        Post("workflow-drafts/{draftId:guid}/scripts");
        AllowAnonymous();
        AllowFileUploads();

        Summary(s =>
        {
            s.Summary = "Upload workflow draft script";
            s.Description = "Uploads or replaces a script file associated to a workflow draft.";
            s.Response<DraftScriptResponse>(StatusCodes.Status201Created, "Script uploaded.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Upload request invalid.");
            s.Response<ApiErrorResponse>(StatusCodes.Status404NotFound, "Draft not found.");
        });
    }

    public override async Task HandleAsync(UploadDraftScriptRequest req, CancellationToken ct)
    {
        var draft = await _engine.GetWorkflowDraftAsync(req.DraftId, ct);
        if (draft is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse("Workflow draft not found."), cancellationToken: ct);
            return;
        }

        var scriptFile = req.Script;
        string? scriptPath = req.ScriptPath;
        if (scriptFile is null && HttpContext.Request.HasFormContentType)
        {
            var form = await HttpContext.Request.ReadFormAsync(ct);
            scriptFile = form.Files.GetFile("script");
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                scriptPath = form.TryGetValue("scriptPath", out var values) ? values.FirstOrDefault() : null;
            }
        }

        if (scriptFile is null || scriptFile.Length == 0)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse("A non-empty script file is required."), cancellationToken: ct);
            return;
        }

        try
        {
            await using var stream = scriptFile.OpenReadStream();
            var savedPath = await _draftScriptStore.SaveScriptAsync(req.DraftId, scriptFile.FileName, stream, scriptPath, ct);
            HttpContext.Response.StatusCode = StatusCodes.Status201Created;
            await HttpContext.Response.WriteAsJsonAsync(new DraftScriptResponse(savedPath), cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(ex.Message), cancellationToken: ct);
        }
    }
}
