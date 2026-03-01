using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Domain;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed record WorkflowVersionResponse(string Name, int Version, int Revision);

public sealed record WorkflowDraftSummaryResponse(
    Guid DraftId,
    string Name,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static WorkflowDraftSummaryResponse FromModel(WorkflowDraftSummary model)
    {
        return new WorkflowDraftSummaryResponse(
            model.DraftId,
            model.Name,
            model.Version,
            model.CreatedAt,
            model.UpdatedAt);
    }
}

public sealed record WorkflowDraftResponse(
    Guid DraftId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    RegisterWorkflowRequest Definition)
{
    public static WorkflowDraftResponse FromModel(WorkflowDraftRecord model)
    {
        return new WorkflowDraftResponse(
            model.DraftId,
            model.CreatedAt,
            model.UpdatedAt,
            RegisterWorkflowRequest.FromDefinition(model.Definition));
    }
}

public sealed record WorkflowDefinitionMetadataResponse(
    string Name,
    int Version,
    int Revision,
    DateTimeOffset RegisteredAt,
    string? Description,
    string? Details,
    WorkflowInputSchemaDefinition InputSchema,
    WorkflowPolicyDefinition Policy)
{
    public static WorkflowDefinitionMetadataResponse FromModel(WorkflowDefinitionMetadata model)
    {
        return new WorkflowDefinitionMetadataResponse(
            model.Name,
            model.Version,
            model.Revision,
            model.RegisteredAt,
            model.Description,
            model.Details,
            model.InputSchema,
            model.Policy);
    }
}

public sealed record SeedWorkflowFileNotFoundResponse(string Message, string Path);

public sealed record RegisterWorkflowRequest
{
    public required string Name { get; init; }

    public required int Version { get; init; }

    public string? Description { get; init; }

    public string? Details { get; init; }

    public WorkflowInputSchemaDefinition? InputSchema { get; init; }

    public WorkflowPolicyDefinition? Policy { get; init; }

    public required IReadOnlyList<WorkflowStepDefinition> Steps { get; init; }

    public WorkflowDefinition ToDefinition()
    {
        return new WorkflowDefinition
        {
            Name = Name,
            Version = Version,
            Description = Description,
            Details = Details,
            InputSchema = InputSchema ?? WorkflowInputSchemaDefinition.Empty,
            Policy = Policy ?? WorkflowPolicyDefinition.Empty,
            Steps = Steps
        };
    }

    public static RegisterWorkflowRequest FromDefinition(WorkflowDefinition definition)
    {
        return new RegisterWorkflowRequest
        {
            Name = definition.Name,
            Version = definition.Version,
            Description = definition.Description,
            Details = definition.Details,
            InputSchema = definition.InputSchema,
            Policy = definition.Policy,
            Steps = definition.Steps
        };
    }
}

public sealed class WorkflowDraftRequest
{
    public required RegisterWorkflowRequest Definition { get; init; }
}

public sealed class WorkflowDraftByIdRequest
{
    [RouteParam]
    [BindFrom("draftId")]
    public Guid DraftId { get; init; }
}

public sealed class UpdateWorkflowDraftRequest
{
    [RouteParam]
    [BindFrom("draftId")]
    public Guid DraftId { get; init; }

    public required RegisterWorkflowRequest Definition { get; init; }
}

public sealed class StartWorkflowInstanceRequest
{
    [RouteParam]
    [BindFrom("workflowName")]
    public string WorkflowName { get; init; } = string.Empty;

    public JsonObject? Inputs { get; init; }

    public int? Version { get; init; }
}

public sealed record DraftScriptResponse(string Path);

public sealed class DraftScriptByPathRequest
{
    [RouteParam]
    [BindFrom("draftId")]
    public Guid DraftId { get; init; }

    [RouteParam]
    [BindFrom("scriptPath")]
    public string ScriptPath { get; init; } = string.Empty;
}

public sealed class UploadDraftScriptRequest
{
    [RouteParam]
    [BindFrom("draftId")]
    public Guid DraftId { get; init; }

    [BindFrom("script")]
    public IFormFile? Script { get; init; }

    [BindFrom("scriptPath")]
    public string? ScriptPath { get; init; }
}
