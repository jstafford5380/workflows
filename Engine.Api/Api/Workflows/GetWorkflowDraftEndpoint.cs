using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class GetWorkflowDraftEndpoint : Endpoint<WorkflowDraftByIdRequest, WorkflowDraftResponse>
{
    private readonly IWorkflowEngineService _engine;

    public GetWorkflowDraftEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Get("workflow-drafts/{draftId:guid}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get workflow draft";
            s.Description = "Returns full workflow draft definition for editing.";
            s.Response<WorkflowDraftResponse>(StatusCodes.Status200OK, "Workflow draft found.");
            s.Response<ApiErrorResponse>(StatusCodes.Status404NotFound, "Draft not found.");
        });

        Description(b => b
            .Produces<WorkflowDraftResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound, "application/json"));
    }

    public override async Task HandleAsync(WorkflowDraftByIdRequest req, CancellationToken ct)
    {
        var draft = await _engine.GetWorkflowDraftAsync(req.DraftId, ct);
        if (draft is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse("Workflow draft not found."), cancellationToken: ct);
            return;
        }

        await HttpContext.Response.WriteAsJsonAsync(WorkflowDraftResponse.FromModel(draft), cancellationToken: ct);
    }
}
