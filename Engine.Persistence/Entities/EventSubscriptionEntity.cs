namespace Engine.Persistence.Entities;

public sealed class EventSubscriptionEntity
{
    public Guid SubscriptionId { get; set; }

    public Guid InstanceId { get; set; }

    public required string StepId { get; set; }

    public required string EventType { get; set; }

    public required string CorrelationKey { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FulfilledAt { get; set; }

    public string? PayloadJson { get; set; }
}
