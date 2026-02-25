namespace Engine.Persistence.Entities;

public sealed class WorkQueueItemEntity
{
    public Guid WorkItemId { get; set; }

    public required string Kind { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public DateTimeOffset? DequeuedAt { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public string? LeaseOwner { get; set; }

    public int DequeueCount { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
