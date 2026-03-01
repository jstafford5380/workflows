using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Domain;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed record WorkflowVersionResponse(string Name, int Version, int Revision);

public sealed record WorkflowDefinitionMetadataResponse(
    string Name,
    int Version,
    int Revision,
    DateTimeOffset RegisteredAt,
    string? Description,
    string? Details,
    WorkflowInputSchemaDefinition InputSchema)
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
            model.InputSchema);
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
            Steps = Steps
        };
    }
}

public sealed class StartWorkflowInstanceRequest
{
    [RouteParam]
    [BindFrom("workflowName")]
    public string WorkflowName { get; init; } = string.Empty;

    public JsonObject? Inputs { get; init; }

    public int? Version { get; init; }
}
