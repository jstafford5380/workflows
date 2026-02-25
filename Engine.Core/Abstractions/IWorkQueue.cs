using Engine.Core.Domain;

namespace Engine.Core.Abstractions;

public interface IWorkQueue
{
    Task EnqueueAsync(WorkQueueItemRecord item, CancellationToken cancellationToken);

    Task<WorkQueueItemRecord?> TryDequeueAsync(
        string leaseOwner,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid workItemId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> RenewLeaseAsync(
        Guid workItemId,
        string leaseOwner,
        DateTimeOffset newLeaseExpiry,
        CancellationToken cancellationToken);
}
