namespace Engine.Persistence.Entities;

public sealed class EventInboxEntity
{
    public required string EventId { get; set; }

    public required string EventType { get; set; }

    public required string CorrelationKey { get; set; }

    public required string PayloadHash { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}
