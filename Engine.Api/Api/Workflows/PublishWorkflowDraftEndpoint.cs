using Engine.Api.Api.Common;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class PublishWorkflowDraftEndpoint : Endpoint<WorkflowDraftByIdRequest, WorkflowVersionResponse>
{
    private readonly IWorkflowEngineService _engine;

    public PublishWorkflowDraftEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("workflow-drafts/{draftId:guid}/publish");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Publish workflow draft";
            s.Description = "Publishes a workflow draft into registered workflow definitions.";
            s.Response<WorkflowVersionResponse>(StatusCodes.Status200OK, "Draft published.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Publish failed due to validation/versioning.");
        });

        Description(b => b
            .Produces<WorkflowVersionResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json"));
    }

    public override async Task HandleAsync(WorkflowDraftByIdRequest req, CancellationToken ct)
    {
        try
        {
            var metadata = await _engine.PublishWorkflowDraftAsync(req.DraftId, ct);
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
