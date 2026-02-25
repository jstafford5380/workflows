namespace Engine.Persistence.Entities;

public sealed class StepExecutionLogEntity
{
    public Guid LogId { get; set; }

    public Guid InstanceId { get; set; }

    public required string StepId { get; set; }

    public int Attempt { get; set; }

    public bool IsSuccess { get; set; }

    public required string ConsoleOutput { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
