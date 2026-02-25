using Engine.Core.Abstractions;
using Engine.Core.Domain;
using Engine.Persistence.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Engine.Persistence.Queue;

public sealed class DbOutbox : IOutbox
{
    private readonly WorkflowDbContext _dbContext;

    public DbOutbox(WorkflowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OutboxMessageRecord>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(x => x.ProcessedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OutboxMessageRecord(
                x.OutboxId,
                Enum.Parse<OutboxMessageType>(x.Type, true),
                PersistenceJson.DeserializeObject(x.PayloadJson),
                x.CreatedAt,
                x.ProcessedAt))
            .ToList();
    }

    public async Task MarkProcessedAsync(IReadOnlyList<Guid> outboxIds, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        if (outboxIds.Count == 0)
        {
            return;
        }

        var rows = await _dbContext.OutboxMessages
            .Where(x => outboxIds.Contains(x.OutboxId) && x.ProcessedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.ProcessedAt = processedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
