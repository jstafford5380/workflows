using FastEndpoints;

namespace Engine.Api.Api.Bundles;

public sealed class PreviewBundleRequest
{
    [BindFrom("bundle")]
    public IFormFile? Bundle { get; init; }
}

public sealed class BundlePreviewByIdRequest
{
    [RouteParam]
    [BindFrom("previewId")]
    public string PreviewId { get; init; } = string.Empty;
}

public sealed class RegisterBundlePreviewRequest
{
    [RouteParam]
    [BindFrom("previewId")]
    public string PreviewId { get; init; } = string.Empty;
}

public sealed record BundleStepPreviewResponse(
    string StepId,
    string DisplayName,
    string ActivityRef,
    bool IsWaitStep,
    string? EventType,
    IReadOnlyList<string> DependsOn,
    string? ResolvedScriptPath,
    bool ScriptExists)
{
    public static BundleStepPreviewResponse FromModel(Engine.Api.Bundles.BundleStepPreview model)
    {
        return new BundleStepPreviewResponse(
            model.StepId,
            model.DisplayName,
            model.ActivityRef,
            model.IsWaitStep,
            model.EventType,
            model.DependsOn,
            model.ResolvedScriptPath,
            model.ScriptExists);
    }
}

public sealed record ExecutionPlanStageResponse(int StageNumber, IReadOnlyList<string> StepIds)
{
    public static ExecutionPlanStageResponse FromModel(Engine.Api.Bundles.ExecutionPlanStage model)
    {
        return new ExecutionPlanStageResponse(model.StageNumber, model.StepIds);
    }
}

public sealed record BundlePreviewApiResponse(
    string PreviewId,
    string BundleFileName,
    string WorkflowName,
    int WorkflowVersion,
    string? WorkflowDescription,
    string? WorkflowDetails,
    IReadOnlyList<BundleStepPreviewResponse> Steps,
    IReadOnlyList<string> Files,
    IReadOnlyList<ExecutionPlanStageResponse> ExecutionPlan,
    bool CanRegister,
    IReadOnlyList<string> ValidationErrors,
    DateTimeOffset ExpiresAt)
{
    public static BundlePreviewApiResponse FromModel(Engine.Api.Bundles.BundlePreviewResponse model)
    {
        return new BundlePreviewApiResponse(
            model.PreviewId,
            model.BundleFileName,
            model.WorkflowName,
            model.WorkflowVersion,
            model.WorkflowDescription,
            model.WorkflowDetails,
            model.Steps.Select(BundleStepPreviewResponse.FromModel).ToList(),
            model.Files,
            model.ExecutionPlan.Select(ExecutionPlanStageResponse.FromModel).ToList(),
            model.CanRegister,
            model.ValidationErrors,
            model.ExpiresAt);
    }
}

public sealed record BundleRegisterApiResponse(
    string BundleId,
    string WorkflowName,
    int WorkflowVersion,
    DateTimeOffset RegisteredAt)
{
    public static BundleRegisterApiResponse FromModel(Engine.Api.Bundles.BundleRegisterResponse model)
    {
        return new BundleRegisterApiResponse(
            model.BundleId,
            model.WorkflowName,
            model.WorkflowVersion,
            model.RegisteredAt);
    }
}
