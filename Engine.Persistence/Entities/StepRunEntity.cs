namespace Engine.Persistence.Entities;

public sealed class StepRunEntity
{
    public Guid InstanceId { get; set; }

    public required string StepId { get; set; }

    public int StepOrder { get; set; }

    public required string DisplayName { get; set; }

    public required string ActivityRef { get; set; }

    public required string StepDefinitionJson { get; set; }

    public required string Status { get; set; }

    public int Attempt { get; set; }

    public required string IdempotencyKey { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public required string OutputsJson { get; set; }

    public WorkflowInstanceEntity? WorkflowInstance { get; set; }
}
