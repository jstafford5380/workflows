using Engine.Core.Abstractions;
using Engine.Core.Domain;
using Engine.Persistence.Entities;
using Engine.Persistence.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Engine.Persistence.Queue;

public sealed class DbWorkQueue : IWorkQueue
{
    private readonly WorkflowDbContext _dbContext;

    public DbWorkQueue(WorkflowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(WorkQueueItemRecord item, CancellationToken cancellationToken)
    {
        _dbContext.WorkQueueItems.Add(new WorkQueueItemEntity
        {
            WorkItemId = item.WorkItemId,
            Kind = item.Kind.ToString(),
            PayloadJson = PersistenceJson.SerializeObject(item.Payload),
            AvailableAt = item.AvailableAt,
            DequeueCount = item.DequeueCount
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkQueueItemRecord?> TryDequeueAsync(
        string leaseOwner,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidateIds = await _dbContext.WorkQueueItems
            .AsNoTracking()
            .Where(x => x.CompletedAt == null
                        && x.AvailableAt <= now
                        && (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now))
            .OrderBy(x => x.AvailableAt)
            .ThenBy(x => x.DequeueCount)
            .Select(x => x.WorkItemId)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var candidateId in candidateIds)
        {
            var row = await _dbContext.WorkQueueItems
                .SingleOrDefaultAsync(x => x.WorkItemId == candidateId
                                           && x.CompletedAt == null
                                           && x.AvailableAt <= now
                                           && (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now), cancellationToken);

            if (row is null)
            {
                continue;
            }

            row.DequeuedAt = now;
            row.LeaseOwner = leaseOwner;
            row.LeaseExpiresAt = leaseExpiresAt;
            row.DequeueCount += 1;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToRecord(row);
        }

        return null;
    }

    public async Task MarkCompletedAsync(Guid workItemId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await _dbContext.WorkQueueItems
            .SingleOrDefaultAsync(x => x.WorkItemId == workItemId && x.CompletedAt == null, cancellationToken);

        if (row is null)
        {
            return;
        }

        row.CompletedAt = now;
        row.LeaseOwner = null;
        row.LeaseExpiresAt = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RenewLeaseAsync(
        Guid workItemId,
        string leaseOwner,
        DateTimeOffset newLeaseExpiry,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.WorkQueueItems
            .SingleOrDefaultAsync(x => x.WorkItemId == workItemId
                                       && x.CompletedAt == null
                                       && x.LeaseOwner == leaseOwner, cancellationToken);

        if (row is null)
        {
            return false;
        }

        row.LeaseExpiresAt = newLeaseExpiry;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static WorkQueueItemRecord ToRecord(WorkQueueItemEntity entity)
    {
        return new WorkQueueItemRecord(
            entity.WorkItemId,
            Enum.Parse<WorkItemKind>(entity.Kind, true),
            PersistenceJson.DeserializeObject(entity.PayloadJson),
            entity.AvailableAt,
            entity.DequeuedAt,
            entity.LeaseExpiresAt,
            entity.LeaseOwner,
            entity.DequeueCount,
            entity.CompletedAt);
    }
}
