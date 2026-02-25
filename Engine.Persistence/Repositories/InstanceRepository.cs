using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Engine.Core.Abstractions;
using Engine.Core.Definitions;
using Engine.Core.Domain;
using Engine.Persistence.Entities;
using Engine.Persistence.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Engine.Persistence.Repositories;

public sealed class InstanceRepository : IInstanceRepository
{
    private static readonly TimeSpan StepLogRetention = TimeSpan.FromDays(30);

    private readonly WorkflowDbContext _dbContext;

    public InstanceRepository(WorkflowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateInstanceAsync(
        WorkflowInstanceRecord instance,
        IReadOnlyList<StepRunRecord> steps,
        IReadOnlyList<StepDependencyRecord> dependencies,
        IReadOnlyList<OutboxMessageRecord> outboxMessages,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var instanceEntity = new WorkflowInstanceEntity
        {
            InstanceId = instance.InstanceId,
            WorkflowName = instance.WorkflowName,
            WorkflowVersion = instance.WorkflowVersion,
            Status = instance.Status.ToString(),
            InputsJson = PersistenceJson.SerializeObject(instance.Inputs),
            CreatedAt = instance.CreatedAt,
            UpdatedAt = instance.UpdatedAt
        };
        _dbContext.WorkflowInstances.Add(instanceEntity);

        _dbContext.StepRuns.AddRange(steps.Select(ToEntity));
        _dbContext.StepDependencies.AddRange(dependencies.Select(d => new StepDependencyEntity
        {
            InstanceId = d.InstanceId,
            StepId = d.StepId,
            DependsOnStepId = d.DependsOnStepId
        }));
        _dbContext.OutboxMessages.AddRange(outboxMessages.Select(ToEntity));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<WorkflowInstanceRecord?> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WorkflowInstances
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<WorkflowInstanceRecord>> ListInstancesAsync(int take, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.WorkflowInstances
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<StepRunRecord>> GetStepRunsAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.StepRuns
            .AsNoTracking()
            .Where(x => x.InstanceId == instanceId)
            .OrderBy(x => x.StepOrder)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<StepExecutionLogRecord>> GetStepExecutionLogsAsync(
        Guid instanceId,
        string stepId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cutoff = now.Subtract(StepLogRetention);

        var expiredLogs = await _dbContext.StepExecutionLogs
            .Where(x => x.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expiredLogs.Count > 0)
        {
            _dbContext.StepExecutionLogs.RemoveRange(expiredLogs);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var rows = await _dbContext.StepExecutionLogs
            .AsNoTracking()
            .Where(x => x.InstanceId == instanceId
                        && x.StepId == stepId
                        && x.CreatedAt >= cutoff)
            .OrderByDescending(x => x.Attempt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new StepExecutionLogRecord(
                x.LogId,
                x.InstanceId,
                x.StepId,
                x.Attempt,
                x.IsSuccess,
                x.ConsoleOutput,
                x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<StepDependencyRecord>> GetDependenciesAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.StepDependencies
            .AsNoTracking()
            .Where(x => x.InstanceId == instanceId)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new StepDependencyRecord(r.InstanceId, r.StepId, r.DependsOnStepId)).ToList();
    }

    public async Task<Dictionary<string, JsonObject>> GetStepOutputsAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.StepRuns
            .AsNoTracking()
            .Where(x => x.InstanceId == instanceId && x.Status == StepRunStatus.Succeeded.ToString())
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.StepId, x => PersistenceJson.DeserializeObject(x.OutputsJson), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<StepRunRecord?> TryClaimRunnableStepAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var step = await _dbContext.StepRuns
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId
                                       && x.StepId == stepId
                                       && x.Status == StepRunStatus.Runnable.ToString()
                                       && (x.NextAttemptAt == null || x.NextAttemptAt <= now)
                                       && (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now), cancellationToken);

        if (step is null)
        {
            return null;
        }

        step.Status = StepRunStatus.Running.ToString();
        step.Attempt += 1;
        step.LeaseOwner = leaseOwner;
        step.LeaseExpiresAt = leaseExpiresAt;
        step.StartedAt ??= now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(step);
    }

    public async Task<bool> RenewStepLeaseAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        DateTimeOffset newLeaseExpiry,
        CancellationToken cancellationToken)
    {
        var step = await _dbContext.StepRuns
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId
                                       && x.StepId == stepId
                                       && x.Status == StepRunStatus.Running.ToString()
                                       && x.LeaseOwner == leaseOwner, cancellationToken);

        if (step is null)
        {
            return false;
        }

        step.LeaseExpiresAt = newLeaseExpiry;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkStepWaitingForEventAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        EventSubscriptionRecord subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var step = await _dbContext.StepRuns
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId
                                       && x.StepId == stepId
                                       && x.Status == StepRunStatus.Running.ToString()
                                       && x.LeaseOwner == leaseOwner, cancellationToken);

        if (step is null)
        {
            throw new InvalidOperationException($"Unable to transition step '{stepId}' to Waiting.");
        }

        step.Status = StepRunStatus.Waiting.ToString();
        step.LeaseOwner = null;
        step.LeaseExpiresAt = null;
        step.LastError = null;

        _dbContext.EventSubscriptions.Add(new EventSubscriptionEntity
        {
            SubscriptionId = subscription.SubscriptionId,
            InstanceId = subscription.InstanceId,
            StepId = subscription.StepId,
            EventType = subscription.EventType,
            CorrelationKey = subscription.CorrelationKey,
            Status = EventSubscriptionStatus.Waiting.ToString(),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task MarkStepSucceededAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        JsonObject outputs,
        DateTimeOffset now,
        IReadOnlyList<OutboxMessageRecord> newOutboxMessages,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var step = await _dbContext.StepRuns
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId
                                       && x.StepId == stepId
                                       && x.Status == StepRunStatus.Running.ToString()
                                       && x.LeaseOwner == leaseOwner, cancellationToken);

        if (step is null)
        {
            throw new InvalidOperationException($"Unable to mark step '{stepId}' as Succeeded.");
        }

        step.Status = StepRunStatus.Succeeded.ToString();
        step.OutputsJson = PersistenceJson.SerializeObject(outputs);
        step.FinishedAt = now;
        step.NextAttemptAt = null;
        step.LeaseOwner = null;
        step.LeaseExpiresAt = null;
        step.LastError = null;

        if (newOutboxMessages.Count > 0)
        {
            _dbContext.OutboxMessages.AddRange(newOutboxMessages.Select(ToEntity));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task MarkStepFailureAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        string error,
        bool shouldRetry,
        bool abortWorkflow,
        DateTimeOffset? nextAttemptAt,
        DateTimeOffset now,
        IReadOnlyList<OutboxMessageRecord> newOutboxMessages,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var step = await _dbContext.StepRuns
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId
                                       && x.StepId == stepId
                                       && x.Status == StepRunStatus.Running.ToString()
                                       && x.LeaseOwner == leaseOwner, cancellationToken);

        if (step is null)
        {
            throw new InvalidOperationException($"Unable to mark step '{stepId}' as Failed/Runnable.");
        }

        step.Status = shouldRetry ? StepRunStatus.Runnable.ToString() : StepRunStatus.Failed.ToString();
        step.LastError = error;
        step.NextAttemptAt = nextAttemptAt;
        step.FinishedAt = shouldRetry ? null : now;
        step.LeaseOwner = null;
        step.LeaseExpiresAt = null;

        if (abortWorkflow)
        {
            var instance = await _dbContext.WorkflowInstances
                .SingleOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken);

            if (instance is not null && instance.Status == WorkflowInstanceStatus.Running.ToString())
            {
                instance.Status = WorkflowInstanceStatus.Failed.ToString();
                instance.UpdatedAt = now;
            }

            var unfinishedSteps = await _dbContext.StepRuns
                .Where(x => x.InstanceId == instanceId
                            && x.StepId != stepId
                            && x.Status != StepRunStatus.Succeeded.ToString()
                            && x.Status != StepRunStatus.Failed.ToString()
                            && x.Status != StepRunStatus.Aborted.ToString())
                .ToListAsync(cancellationToken);

            foreach (var unfinishedStep in unfinishedSteps)
            {
                unfinishedStep.Status = StepRunStatus.Aborted.ToString();
                unfinishedStep.FinishedAt = now;
                unfinishedStep.LeaseOwner = null;
                unfinishedStep.LeaseExpiresAt = null;
                unfinishedStep.NextAttemptAt = null;
            }

            var waitingSubscriptions = await _dbContext.EventSubscriptions
                .Where(x => x.InstanceId == instanceId && x.Status == EventSubscriptionStatus.Waiting.ToString())
                .ToListAsync(cancellationToken);

            foreach (var waitingSubscription in waitingSubscriptions)
            {
                waitingSubscription.Status = EventSubscriptionStatus.Canceled.ToString();
                waitingSubscription.FulfilledAt = now;
            }
        }
        else if (!shouldRetry)
        {
            var instance = await _dbContext.WorkflowInstances
                .SingleOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken);

            if (instance is not null && instance.Status == WorkflowInstanceStatus.Running.ToString())
            {
                instance.Status = WorkflowInstanceStatus.Failed.ToString();
                instance.UpdatedAt = now;
            }
        }

        if (newOutboxMessages.Count > 0)
        {
            _dbContext.OutboxMessages.AddRange(newOutboxMessages.Select(ToEntity));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task SaveStepExecutionLogAsync(
        Guid instanceId,
        string stepId,
        int attempt,
        bool isSuccess,
        string consoleOutput,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.StepExecutionLogs.Add(new StepExecutionLogEntity
        {
            LogId = Guid.NewGuid(),
            InstanceId = instanceId,
            StepId = stepId,
            Attempt = attempt,
            IsSuccess = isSuccess,
            ConsoleOutput = consoleOutput,
            CreatedAt = now
        });

        var cutoff = now.Subtract(StepLogRetention);
        var expiredLogs = await _dbContext.StepExecutionLogs
            .Where(x => x.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expiredLogs.Count > 0)
        {
            _dbContext.StepExecutionLogs.RemoveRange(expiredLogs);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<bool> TryCancelInstanceAsync(Guid instanceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var instance = await _dbContext.WorkflowInstances
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken);

        if (instance is null
            || instance.Status == WorkflowInstanceStatus.Succeeded.ToString()
            || instance.Status == WorkflowInstanceStatus.Failed.ToString()
            || instance.Status == WorkflowInstanceStatus.Canceled.ToString())
        {
            return false;
        }

        instance.Status = WorkflowInstanceStatus.Canceled.ToString();
        instance.UpdatedAt = now;

        var steps = await _dbContext.StepRuns
            .Where(x => x.InstanceId == instanceId
                        && x.Status != StepRunStatus.Succeeded.ToString()
                        && x.Status != StepRunStatus.Failed.ToString()
                        && x.Status != StepRunStatus.Aborted.ToString())
            .ToListAsync(cancellationToken);

        foreach (var step in steps)
        {
            step.Status = StepRunStatus.Aborted.ToString();
            step.FinishedAt = now;
            step.LeaseOwner = null;
            step.LeaseExpiresAt = null;
            step.NextAttemptAt = null;
        }

        var waitingSubscriptions = await _dbContext.EventSubscriptions
            .Where(x => x.InstanceId == instanceId && x.Status == EventSubscriptionStatus.Waiting.ToString())
            .ToListAsync(cancellationToken);

        foreach (var subscription in waitingSubscriptions)
        {
            subscription.Status = EventSubscriptionStatus.Canceled.ToString();
            subscription.FulfilledAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RetryStepAsync(
        Guid instanceId,
        string stepId,
        DateTimeOffset now,
        OutboxMessageRecord retryOutboxMessage,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var step = await _dbContext.StepRuns
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId
                                       && x.StepId == stepId
                                       && (x.Status == StepRunStatus.Failed.ToString()
                                           || x.Status == StepRunStatus.Canceled.ToString()
                                           || x.Status == StepRunStatus.Aborted.ToString()), cancellationToken);

        if (step is null)
        {
            return false;
        }

        step.Status = StepRunStatus.Runnable.ToString();
        step.NextAttemptAt = now;
        step.LeaseOwner = null;
        step.LeaseExpiresAt = null;
        step.LastError = null;
        step.FinishedAt = null;

        var instance = await _dbContext.WorkflowInstances
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken);

        if (instance is not null && (instance.Status == WorkflowInstanceStatus.Failed.ToString()
                                     || instance.Status == WorkflowInstanceStatus.Paused.ToString()
                                     || instance.Status == WorkflowInstanceStatus.Canceled.ToString()))
        {
            instance.Status = WorkflowInstanceStatus.Running.ToString();
            instance.UpdatedAt = now;
        }

        _dbContext.OutboxMessages.Add(ToEntity(retryOutboxMessage));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<EventIngestResult> IngestExternalEventAsync(
        ExternalEventEnvelope externalEvent,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var payloadHash = externalEvent.PayloadHash ?? ComputePayloadHash(externalEvent.Payload);
        var duplicate = await _dbContext.EventInboxes
            .AsNoTracking()
            .AnyAsync(x => x.EventId == externalEvent.EventId, cancellationToken);

        if (duplicate)
        {
            return new EventIngestResult(true, 0);
        }

        _dbContext.EventInboxes.Add(new EventInboxEntity
        {
            EventId = externalEvent.EventId,
            EventType = externalEvent.EventType,
            CorrelationKey = externalEvent.CorrelationKey,
            PayloadHash = payloadHash,
            PayloadJson = PersistenceJson.SerializeObject(externalEvent.Payload),
            ReceivedAt = now
        });

        var subscriptions = await _dbContext.EventSubscriptions
            .Where(x => x.Status == EventSubscriptionStatus.Waiting.ToString()
                        && x.EventType == externalEvent.EventType
                        && x.CorrelationKey == externalEvent.CorrelationKey)
            .ToListAsync(cancellationToken);

        var fulfilledCount = 0;
        foreach (var subscription in subscriptions)
        {
            subscription.Status = EventSubscriptionStatus.Fulfilled.ToString();
            subscription.FulfilledAt = now;
            subscription.PayloadJson = PersistenceJson.SerializeObject(externalEvent.Payload);

            var stepRun = await _dbContext.StepRuns
                .SingleAsync(x => x.InstanceId == subscription.InstanceId && x.StepId == subscription.StepId, cancellationToken);

            stepRun.Status = StepRunStatus.Succeeded.ToString();
            stepRun.OutputsJson = PersistenceJson.SerializeObject(externalEvent.Payload);
            stepRun.FinishedAt = now;
            stepRun.LastError = null;
            stepRun.LeaseOwner = null;
            stepRun.LeaseExpiresAt = null;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var outbox = await PromoteDependentsToRunnableInternalAsync(subscription.InstanceId, subscription.StepId, now, cancellationToken);
            if (outbox.Count > 0)
            {
                _dbContext.OutboxMessages.AddRange(outbox.Select(ToEntity));
            }

            await MarkInstanceSucceededIfCompleteInternalAsync(subscription.InstanceId, now, cancellationToken);
            fulfilledCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new EventIngestResult(false, fulfilledCount);
    }

    public async Task MarkInstanceSucceededIfCompleteAsync(Guid instanceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await MarkInstanceSucceededIfCompleteInternalAsync(instanceId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> PromoteDependentsToRunnableAsync(
        Guid instanceId,
        string completedStepId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var outbox = await PromoteDependentsToRunnableInternalAsync(instanceId, completedStepId, now, cancellationToken);
        if (outbox.Count > 0)
        {
            _dbContext.OutboxMessages.AddRange(outbox.Select(ToEntity));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return outbox
            .Select(x => x.Payload["stepId"]?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }

    private async Task MarkInstanceSucceededIfCompleteInternalAsync(Guid instanceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hasIncomplete = await _dbContext.StepRuns
            .AnyAsync(x => x.InstanceId == instanceId && x.Status != StepRunStatus.Succeeded.ToString(), cancellationToken);

        if (hasIncomplete)
        {
            return;
        }

        var instance = await _dbContext.WorkflowInstances
            .SingleOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken);

        if (instance is not null && instance.Status == WorkflowInstanceStatus.Running.ToString())
        {
            instance.Status = WorkflowInstanceStatus.Succeeded.ToString();
            instance.UpdatedAt = now;
        }
    }

    private async Task<IReadOnlyList<OutboxMessageRecord>> PromoteDependentsToRunnableInternalAsync(
        Guid instanceId,
        string completedStepId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dependentStepIds = await _dbContext.StepDependencies
            .Where(x => x.InstanceId == instanceId && x.DependsOnStepId == completedStepId)
            .Select(x => x.StepId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var outbox = new List<OutboxMessageRecord>();
        foreach (var dependentStepId in dependentStepIds)
        {
            var dependentStep = await _dbContext.StepRuns
                .SingleAsync(x => x.InstanceId == instanceId && x.StepId == dependentStepId, cancellationToken);

            if (dependentStep.Status != StepRunStatus.Pending.ToString())
            {
                continue;
            }

            var deps = await _dbContext.StepDependencies
                .Where(x => x.InstanceId == instanceId && x.StepId == dependentStepId)
                .Select(x => x.DependsOnStepId)
                .ToListAsync(cancellationToken);

            var unmetDependencies = await _dbContext.StepRuns
                .Where(x => x.InstanceId == instanceId
                            && deps.Contains(x.StepId)
                            && x.Status != StepRunStatus.Succeeded.ToString())
                .AnyAsync(cancellationToken);

            if (unmetDependencies)
            {
                continue;
            }

            dependentStep.Status = StepRunStatus.Runnable.ToString();
            dependentStep.NextAttemptAt = now;
            dependentStep.LastError = null;

            var payload = new JsonObject
            {
                ["instanceId"] = instanceId.ToString(),
                ["stepId"] = dependentStepId,
                ["availableAt"] = now
            };

            outbox.Add(new OutboxMessageRecord(
                Guid.NewGuid(),
                OutboxMessageType.EnqueueWorkItem,
                payload,
                now,
                null));
        }

        return outbox;
    }

    private static WorkflowInstanceRecord ToRecord(WorkflowInstanceEntity entity)
    {
        return new WorkflowInstanceRecord(
            entity.InstanceId,
            entity.WorkflowName,
            entity.WorkflowVersion,
            Enum.Parse<WorkflowInstanceStatus>(entity.Status, true),
            PersistenceJson.DeserializeObject(entity.InputsJson),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static StepRunRecord ToRecord(StepRunEntity entity)
    {
        return new StepRunRecord(
            entity.InstanceId,
            entity.StepId,
            entity.DisplayName,
            entity.ActivityRef,
            Enum.Parse<StepRunStatus>(entity.Status, true),
            entity.Attempt,
            entity.StepOrder,
            entity.IdempotencyKey,
            entity.StartedAt,
            entity.FinishedAt,
            entity.NextAttemptAt,
            entity.LeaseExpiresAt,
            entity.LeaseOwner,
            entity.LastError,
            PersistenceJson.DeserializeObject(entity.OutputsJson),
            PersistenceJson.Deserialize<WorkflowStepDefinition>(entity.StepDefinitionJson));
    }

    private static StepRunEntity ToEntity(StepRunRecord step)
    {
        return new StepRunEntity
        {
            InstanceId = step.InstanceId,
            StepId = step.StepId,
            StepOrder = step.StepOrder,
            DisplayName = step.DisplayName,
            ActivityRef = step.ActivityRef,
            StepDefinitionJson = PersistenceJson.Serialize(step.StepDefinition),
            Status = step.Status.ToString(),
            Attempt = step.Attempt,
            IdempotencyKey = step.IdempotencyKey,
            LeaseOwner = step.LeaseOwner,
            LeaseExpiresAt = step.LeaseExpiresAt,
            StartedAt = step.StartedAt,
            FinishedAt = step.FinishedAt,
            NextAttemptAt = step.NextAttemptAt,
            LastError = step.LastError,
            OutputsJson = PersistenceJson.SerializeObject(step.Outputs)
        };
    }

    private static OutboxMessageEntity ToEntity(OutboxMessageRecord outbox)
    {
        return new OutboxMessageEntity
        {
            OutboxId = outbox.OutboxId,
            Type = outbox.Type.ToString(),
            PayloadJson = PersistenceJson.SerializeObject(outbox.Payload),
            CreatedAt = outbox.CreatedAt,
            ProcessedAt = outbox.ProcessedAt
        };
    }

    private static string ComputePayloadHash(JsonObject payload)
    {
        var json = PersistenceJson.SerializeObject(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}
