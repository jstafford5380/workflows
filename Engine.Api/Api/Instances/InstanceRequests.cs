using FastEndpoints;

namespace Engine.Api.Api.Instances;

public sealed class GetInstanceRequest
{
    [RouteParam]
    [BindFrom("instanceId")]
    public Guid InstanceId { get; init; }
}

public sealed class CancelInstanceRequest
{
    [RouteParam]
    [BindFrom("instanceId")]
    public Guid InstanceId { get; init; }
}

public sealed class RetryStepRequest
{
    [RouteParam]
    [BindFrom("instanceId")]
    public Guid InstanceId { get; init; }

    [RouteParam]
    [BindFrom("stepId")]
    public string StepId { get; init; } = string.Empty;
}
