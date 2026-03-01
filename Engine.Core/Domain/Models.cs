using System.Text.Json.Nodes;
using Engine.Core.Definitions;

namespace Engine.Core.Domain;

public sealed record WorkflowDefinitionMetadata(
    string Name,
    int Version,
    int Revision,
    DateTimeOffset RegisteredAt,
    string? Description,
    string? Details,
    WorkflowInputSchemaDefinition InputSchema,
    WorkflowPolicyDefinition Policy);

public sealed record WorkflowDraftSummary(
    Guid DraftId,
    string Name,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkflowDraftRecord(
    Guid DraftId,
    WorkflowDefinition Definition,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkflowInstanceRecord(
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    WorkflowInstanceStatus Status,
    JsonObject Inputs,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StepRunRecord(
    Guid InstanceId,
    string StepId,
    string DisplayName,
    string ActivityRef,
    StepRunStatus Status,
    int Attempt,
    int StepOrder,
    string IdempotencyKey,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LeaseExpiresAt,
    string? LeaseOwner,
    string? LastError,
    JsonObject Outputs,
    WorkflowStepDefinition StepDefinition);

public sealed record StepDependencyRecord(Guid InstanceId, string StepId, string DependsOnStepId);

public sealed record EventSubscriptionRecord(
    Guid SubscriptionId,
    Guid InstanceId,
    string StepId,
    string EventType,
    string CorrelationKey,
    EventSubscriptionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FulfilledAt,
    JsonObject? Payload);

public sealed record OutboxMessageRecord(
    Guid OutboxId,
    OutboxMessageType Type,
    JsonObject Payload,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public sealed record WorkQueueItemRecord(
    Guid WorkItemId,
    WorkItemKind Kind,
    JsonObject Payload,
    DateTimeOffset AvailableAt,
    DateTimeOffset? DequeuedAt,
    DateTimeOffset? LeaseExpiresAt,
    string? LeaseOwner,
    int DequeueCount,
    DateTimeOffset? CompletedAt);

public sealed record StepExecutionLogRecord(
    Guid LogId,
    Guid InstanceId,
    string StepId,
    int Attempt,
    bool IsSuccess,
    string ConsoleOutput,
    DateTimeOffset CreatedAt);

public sealed record ActivityExecutionRequest(
    Guid InstanceId,
    string StepId,
    string ActivityRef,
    JsonObject Inputs,
    string IdempotencyKey,
    IReadOnlyList<ScriptParameterDefinition> ScriptParameters);

public sealed record ActivityExecutionResult(
    bool IsSuccess,
    JsonObject Outputs,
    string? ErrorMessage,
    bool IsRetryable = true,
    string? ConsoleOutput = null);

public sealed record ExternalEventEnvelope(
    string EventId,
    string EventType,
    string CorrelationKey,
    JsonObject Payload,
    string? PayloadHash = null);

public sealed record EventIngestResult(bool IsDuplicate, int FulfilledSubscriptions);

public sealed record ChecklistStepView(
    string StepId,
    string DisplayName,
    StepRunStatus Status,
    int Attempt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> BlockedBy,
    string? LastError,
    IReadOnlyList<string> OutputKeys,
    IReadOnlyDictionary<string, bool> SafetyMetadata);

public sealed record WorkflowInstanceChecklistView(
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    WorkflowInstanceStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    JsonObject Inputs,
    IReadOnlyList<ChecklistStepView> Steps);

public sealed record WorkflowInstanceSummaryView(
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    WorkflowInstanceStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StepExecutionLogView(
    int Attempt,
    bool IsSuccess,
    string ConsoleOutput,
    DateTimeOffset CreatedAt);

public sealed record ApprovalCommentRecord(
    string Author,
    string Comment,
    DateTimeOffset At);

public sealed record ApprovalRequestView(
    Guid ApprovalId,
    Guid InstanceId,
    string WorkflowName,
    int WorkflowVersion,
    string StepId,
    string EventType,
    string CorrelationKey,
    ApprovalRequestStatus Status,
    string? Assignee,
    string? Reason,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<ApprovalCommentRecord> Comments);

public sealed record ApprovalDecisionResult(
    bool Applied,
    ApprovalRequestView? Approval,
    ExternalEventEnvelope? ResolutionEvent);

public sealed record AuditEventView(
    Guid AuditId,
    string Category,
    string Action,
    Guid? InstanceId,
    string? WorkflowName,
    string? StepId,
    string Actor,
    JsonObject Details,
    DateTimeOffset CreatedAt);
