using Engine.Core.Abstractions;
using Engine.Core.Domain;
using Engine.Runtime.Workers;
using Microsoft.Extensions.Logging;

namespace Engine.Runtime.Services;

public sealed class OutboxDispatcher
{
    private readonly IOutbox _outbox;
    private readonly IWorkQueue _workQueue;
    private readonly IClock _clock;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IOutbox outbox,
        IWorkQueue workQueue,
        IClock clock,
        ILogger<OutboxDispatcher> logger)
    {
        _outbox = outbox;
        _workQueue = workQueue;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> DispatchBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var messages = await _outbox.GetUnprocessedAsync(batchSize, cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            if (message.Type != OutboxMessageType.EnqueueWorkItem)
            {
                _logger.LogInformation("Skipping unsupported outbox message type {Type}", message.Type);
                continue;
            }

            var payload = WorkItemPayload.FromJson(message.Payload);
            var item = new WorkQueueItemRecord(
                Guid.NewGuid(),
                WorkItemKind.ExecuteStep,
                payload.ToJson(),
                payload.AvailableAt,
                null,
                null,
                null,
                0,
                null);

            await _workQueue.EnqueueAsync(item, cancellationToken);
        }

        await _outbox.MarkProcessedAsync(messages.Select(x => x.OutboxId).ToList(), _clock.UtcNow, cancellationToken);
        return messages.Count;
    }
}
