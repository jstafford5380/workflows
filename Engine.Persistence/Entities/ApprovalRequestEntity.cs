namespace Engine.Persistence.Entities;

public sealed class ApprovalRequestEntity
{
    public Guid ApprovalId { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid InstanceId { get; set; }

    public required string StepId { get; set; }

    public required string EventType { get; set; }

    public required string CorrelationKey { get; set; }

    public required string Status { get; set; }

    public string? Assignee { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string CommentsJson { get; set; } = "[]";
}
