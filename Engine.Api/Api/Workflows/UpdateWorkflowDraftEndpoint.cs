using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class UpdateWorkflowDraftEndpoint : Endpoint<UpdateWorkflowDraftRequest, WorkflowDraftSummaryResponse>
{
    private readonly IWorkflowEngineService _engine;

    public UpdateWorkflowDraftEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Put("workflow-drafts/{draftId:guid}");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Update workflow draft";
            s.Description = "Updates an existing workflow draft definition.";
            s.RequestParam(r => r.DraftId, "Draft id.");
            s.Response<WorkflowDraftSummaryResponse>(StatusCodes.Status200OK, "Draft updated.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Draft payload invalid.");
        });

        Description(b => b
            .Accepts<WorkflowDraftRequest>("application/json")
            .Produces<WorkflowDraftSummaryResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(UpdateWorkflowDraftRequest req, CancellationToken ct)
    {
        try
        {
            var summary = await _engine.SaveWorkflowDraftAsync(req.DraftId, req.Definition.ToDefinition(), ct);
            await HttpContext.Response.WriteAsJsonAsync(WorkflowDraftSummaryResponse.FromModel(summary), cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(ex.Message), cancellationToken: ct);
        }
    }
}
