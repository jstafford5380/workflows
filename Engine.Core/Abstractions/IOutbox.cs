using Engine.Core.Domain;

namespace Engine.Core.Abstractions;

public interface IOutbox
{
    Task<IReadOnlyList<OutboxMessageRecord>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkProcessedAsync(IReadOnlyList<Guid> outboxIds, DateTimeOffset processedAt, CancellationToken cancellationToken);
}
