namespace Engine.Persistence.Entities;

public sealed class WorkflowInstanceEntity
{
    public Guid InstanceId { get; set; }

    public required string WorkflowName { get; set; }

    public int WorkflowVersion { get; set; }

    public required string Status { get; set; }

    public required string InputsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<StepRunEntity> StepRuns { get; set; } = [];
}
