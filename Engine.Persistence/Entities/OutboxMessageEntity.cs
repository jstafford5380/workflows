namespace Engine.Persistence.Entities;

public sealed class OutboxMessageEntity
{
    public Guid OutboxId { get; set; }

    public required string Type { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
