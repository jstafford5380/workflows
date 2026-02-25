using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Domain;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed record WorkflowVersionResponse(string Name, int Version);

public sealed record WorkflowDefinitionMetadataResponse(string Name, int Version, DateTimeOffset RegisteredAt)
{
    public static WorkflowDefinitionMetadataResponse FromModel(WorkflowDefinitionMetadata model)
    {
        return new WorkflowDefinitionMetadataResponse(model.Name, model.Version, model.RegisteredAt);
    }
}

public sealed record SeedWorkflowFileNotFoundResponse(string Message, string Path);

public sealed record RegisterWorkflowRequest
{
    public required string Name { get; init; }

    public required int Version { get; init; }

    public string? Description { get; init; }

    public string? Details { get; init; }

    public required IReadOnlyList<WorkflowStepDefinition> Steps { get; init; }

    public WorkflowDefinition ToDefinition()
    {
        return new WorkflowDefinition
        {
            Name = Name,
            Version = Version,
            Description = Description,
            Details = Details,
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
