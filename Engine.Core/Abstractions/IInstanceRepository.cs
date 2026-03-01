using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Domain;

namespace Engine.Core.Abstractions;

public interface IInstanceRepository
{
    Task CreateInstanceAsync(
        WorkflowInstanceRecord instance,
        IReadOnlyList<StepRunRecord> steps,
        IReadOnlyList<StepDependencyRecord> dependencies,
        IReadOnlyList<OutboxMessageRecord> outboxMessages,
        CancellationToken cancellationToken);

    Task<WorkflowInstanceRecord?> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowInstanceRecord>> ListInstancesAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<StepRunRecord>> GetStepRunsAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StepExecutionLogRecord>> GetStepExecutionLogsAsync(
        Guid instanceId,
        string stepId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StepDependencyRecord>> GetDependenciesAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<Dictionary<string, JsonObject>> GetStepOutputsAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<StepRunRecord?> TryClaimRunnableStepAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<bool> RenewStepLeaseAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        DateTimeOffset newLeaseExpiry,
        CancellationToken cancellationToken);

    Task MarkStepWaitingForEventAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        EventSubscriptionRecord subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkStepSucceededAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        JsonObject outputs,
        DateTimeOffset now,
        IReadOnlyList<OutboxMessageRecord> newOutboxMessages,
        CancellationToken cancellationToken);

    Task MarkStepFailureAsync(
        Guid instanceId,
        string stepId,
        string leaseOwner,
        string error,
        bool shouldRetry,
        bool abortWorkflow,
        DateTimeOffset? nextAttemptAt,
        DateTimeOffset now,
        IReadOnlyList<OutboxMessageRecord> newOutboxMessages,
        CancellationToken cancellationToken);

    Task SaveStepExecutionLogAsync(
        Guid instanceId,
        string stepId,
        int attempt,
        bool isSuccess,
        string consoleOutput,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<bool> TryCancelInstanceAsync(Guid instanceId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> RetryStepAsync(
        Guid instanceId,
        string stepId,
        DateTimeOffset now,
        OutboxMessageRecord retryOutboxMessage,
        CancellationToken cancellationToken);

    Task<EventIngestResult> IngestExternalEventAsync(
        ExternalEventEnvelope externalEvent,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalRequestView>> ListApprovalRequestsAsync(
        ApprovalRequestStatus? status,
        Guid? instanceId,
        string? workflowName,
        string? assignee,
        string? stepId,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ApprovalRequestView?> GetApprovalRequestAsync(Guid approvalId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ApprovalRequestView?> UpdateApprovalMetadataAsync(
        Guid approvalId,
        string? assignee,
        string? reason,
        DateTimeOffset? expiresAt,
        ApprovalCommentRecord? comment,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ApprovalRequestView?> AddApprovalCommentAsync(
        Guid approvalId,
        ApprovalCommentRecord comment,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ApprovalDecisionResult> ResolveApprovalAsync(
        Guid approvalId,
        bool approved,
        ApprovalCommentRecord? comment,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEventView>> ListAuditEventsAsync(
        int take,
        Guid? instanceId,
        string? workflowName,
        string? category,
        string? action,
        string? actor,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        CancellationToken cancellationToken);

    Task AppendAuditEventAsync(AuditEventView auditEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> PromoteDependentsToRunnableAsync(
        Guid instanceId,
        string completedStepId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkInstanceSucceededIfCompleteAsync(Guid instanceId, DateTimeOffset now, CancellationToken cancellationToken);
}
