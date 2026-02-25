namespace Engine.Persistence.Entities;

public sealed class StepDependencyEntity
{
    public Guid InstanceId { get; set; }

    public required string StepId { get; set; }

    public required string DependsOnStepId { get; set; }
}
