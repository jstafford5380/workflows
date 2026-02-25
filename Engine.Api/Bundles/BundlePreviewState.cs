using Engine.Core.Definitions;

namespace Engine.Api.Bundles;

internal sealed record BundlePreviewState(
    string PreviewId,
    string BundleFileName,
    string PreviewRoot,
    string ExtractedRoot,
    WorkflowDefinition WorkflowDefinition,
    IReadOnlyList<string> Files,
    IReadOnlyDictionary<string, string> StepScriptPaths,
    IReadOnlyList<ExecutionPlanStage> ExecutionPlan,
    IReadOnlyList<BundleStepPreview> Steps,
    IReadOnlyList<string> ValidationErrors,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
