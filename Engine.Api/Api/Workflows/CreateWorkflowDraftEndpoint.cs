using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class CreateWorkflowDraftEndpoint : Endpoint<WorkflowDraftRequest, WorkflowDraftSummaryResponse>
{
    private readonly IWorkflowEngineService _engine;

    public CreateWorkflowDraftEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("workflow-drafts");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Create workflow draft";
            s.Description = "Creates a new workflow draft from a workflow definition payload.";
            s.Response<WorkflowDraftSummaryResponse>(StatusCodes.Status201Created, "Draft created.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Draft payload invalid.");
        });

        Description(b => b
            .Accepts<WorkflowDraftRequest>("application/json")
            .Produces<WorkflowDraftSummaryResponse>(StatusCodes.Status201Created, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(WorkflowDraftRequest req, CancellationToken ct)
    {
        try
        {
            var summary = await _engine.SaveWorkflowDraftAsync(null, req.Definition.ToDefinition(), ct);
            HttpContext.Response.StatusCode = StatusCodes.Status201Created;
            HttpContext.Response.Headers.Location = $"/workflow-drafts/{summary.DraftId}";
            await HttpContext.Response.WriteAsJsonAsync(WorkflowDraftSummaryResponse.FromModel(summary), cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(ex.Message), cancellationToken: ct);
        }
    }
}
