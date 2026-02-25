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
    DateTimeOffset RegisteredAt);

public sealed record WorkflowDefinitionMetadata(
    string Name,
    int Version,
    DateTimeOffset RegisteredAt);

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
