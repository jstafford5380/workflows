namespace Engine.Persistence.Entities;

public sealed class AuditEventEntity
{
    public Guid AuditId { get; set; }

    public required string Category { get; set; }

    public required string Action { get; set; }

    public Guid? InstanceId { get; set; }

    public string? WorkflowName { get; set; }

    public string? StepId { get; set; }

    public required string Actor { get; set; }

    public string DetailsJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
