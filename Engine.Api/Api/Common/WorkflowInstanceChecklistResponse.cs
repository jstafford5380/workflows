using System.Text.Json.Nodes;
using Engine.Core.Domain;

namespace Engine.Api.Api.Common;

public sealed record ChecklistStepResponse(
    string StepId,
    string DisplayName,
    StepRunStatus Status,
    int Attempt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> BlockedBy,
    string? LastError,
    IReadOnlyList<string> OutputKeys,
    IReadOnlyDictionary<string, bool> SafetyMetadata)
{
    public static ChecklistStepResponse FromModel(ChecklistStepView model)
    {
        return new ChecklistStepResponse(
            model.StepId,
            model.DisplayName,
            model.Status,
            model.Attempt,
            model.StartedAt,
            model.FinishedAt,
            model.DependsOn,
            model.BlockedBy,
            model.LastError,
            model.OutputKeys,
            model.SafetyMetadata);
    }
}

public sealed record WorkflowInstanceChecklistResponse(
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    WorkflowInstanceStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    JsonObject Inputs,
    IReadOnlyList<ChecklistStepResponse> Steps)
{
    public static WorkflowInstanceChecklistResponse FromModel(WorkflowInstanceChecklistView model)
    {
        return new WorkflowInstanceChecklistResponse(
            model.InstanceId,
            model.WorkflowName,
            model.WorkflowVersion,
            model.Status,
            model.CreatedAt,
            model.UpdatedAt,
            model.Inputs,
            model.Steps.Select(ChecklistStepResponse.FromModel).ToList());
    }
}
