using System.Text.Json.Nodes;

namespace Engine.BundleUi.Models;

public sealed record BundleStepPreview(
    string StepId,
    string DisplayName,
    string ActivityRef,
    bool IsWaitStep,
    string? EventType,
    IReadOnlyList<string> DependsOn,
    string? ResolvedScriptPath,
    bool ScriptExists);

public sealed record ExecutionPlanStage(int StageNumber, IReadOnlyList<string> StepIds);

public sealed record BundlePreviewResponse(
    string PreviewId,
    string BundleFileName,
    string WorkflowName,
    int WorkflowVersion,
    string? WorkflowDescription,
    string? WorkflowDetails,
    WorkflowInputSchema WorkflowInputSchema,
    IReadOnlyList<BundleStepPreview> Steps,
    IReadOnlyList<string> Files,
    IReadOnlyList<ExecutionPlanStage> ExecutionPlan,
    bool CanRegister,
    IReadOnlyList<string> ValidationErrors,
    DateTimeOffset ExpiresAt);

public sealed record BundleRegisterResponse(
    string BundleId,
    string WorkflowName,
    int WorkflowVersion,
    int WorkflowRevision,
    DateTimeOffset RegisteredAt);

public sealed record WorkflowDefinitionMetadata(
    string Name,
    int Version,
    int Revision,
    DateTimeOffset RegisteredAt,
    string? Description,
    string? Details,
    WorkflowInputSchema InputSchema);

public sealed record WorkflowDraftSummary(
    Guid DraftId,
    string Name,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkflowDraft(
    Guid DraftId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    WorkflowDraftDefinition Definition);

public sealed record WorkflowDraftDefinition(
    string Name,
    int Version,
    string? Description,
    string? Details,
    WorkflowInputSchema InputSchema,
    IReadOnlyList<WorkflowStepDraftDefinition> Steps);

public sealed record WorkflowStepDraftDefinition(
    string StepId,
    string DisplayName,
    string ActivityRef,
    Dictionary<string, WorkflowInputBindingValue> Inputs,
    IReadOnlyList<string> OutputsSchema,
    RetryPolicyDraftDefinition RetryPolicy,
    int? TimeoutSeconds,
    WaitForEventDraftDefinition? WaitForEvent,
    IReadOnlyList<ScriptParameterDraftDefinition> ScriptParameters,
    bool AbortOnFail,
    Dictionary<string, bool> SafetyMetadata);

public sealed record WorkflowInputBindingValue(string? Binding, JsonNode? Literal);

public sealed record RetryPolicyDraftDefinition(
    int MaxAttempts,
    int InitialDelaySeconds,
    int MaxDelaySeconds,
    double BackoffFactor);

public sealed record WaitForEventDraftDefinition(string EventType, string CorrelationKeyExpression);

public sealed record ScriptParameterDraftDefinition(string Name, bool Required);

public sealed record WorkflowInputSchema(IReadOnlyList<WorkflowInputField> Fields)
{
    public static WorkflowInputSchema Empty { get; } = new([]);
}

public sealed record WorkflowInputField(
    string Name,
    string? DisplayName,
    string Type,
    bool Required,
    string? Description,
    string? Placeholder,
    JsonNode? DefaultValue,
    bool IsSecret,
    IReadOnlyList<string> Options);

public sealed record WorkflowInstanceStep(
    string StepId,
    string DisplayName,
    string Status,
    int Attempt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> BlockedBy,
    string? LastError,
    IReadOnlyList<string> OutputKeys);

public sealed record WorkflowInstanceChecklist(
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowInstanceStep> Steps);

public sealed record StepExecutionLog(
    int Attempt,
    bool IsSuccess,
    string ConsoleOutput,
    DateTimeOffset CreatedAt);

public sealed record WorkflowInstanceSummary(
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
