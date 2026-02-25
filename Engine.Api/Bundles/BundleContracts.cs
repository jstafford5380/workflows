namespace Engine.Api.Bundles;

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
