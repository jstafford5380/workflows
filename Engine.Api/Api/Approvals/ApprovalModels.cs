using System.Text.Json.Nodes;
using Engine.Core.Domain;
using FastEndpoints;

namespace Engine.Api.Api.Approvals;

public sealed record ApprovalCommentResponse(string Author, string Comment, DateTimeOffset At)
{
    public static ApprovalCommentResponse FromModel(ApprovalCommentRecord model) => new(model.Author, model.Comment, model.At);
}

public sealed record ApprovalResponse(
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
    IReadOnlyList<ApprovalCommentResponse> Comments)
{
    public static ApprovalResponse FromModel(ApprovalRequestView model) => new(
        model.ApprovalId,
        model.InstanceId,
        model.WorkflowName,
        model.WorkflowVersion,
        model.StepId,
        model.EventType,
        model.CorrelationKey,
        model.Status,
        model.Assignee,
        model.Reason,
        model.ExpiresAt,
        model.CreatedAt,
        model.UpdatedAt,
        model.ResolvedAt,
        model.Comments.Select(ApprovalCommentResponse.FromModel).ToList());
}

public sealed class ListApprovalsRequest
{
    [QueryParam]
    public string? Status { get; init; }

    [QueryParam]
    public Guid? InstanceId { get; init; }

    [QueryParam]
    public string? WorkflowName { get; init; }

    [QueryParam]
    public string? Assignee { get; init; }

    [QueryParam]
    public string? StepId { get; init; }

    [QueryParam]
    public DateTimeOffset? CreatedAfter { get; init; }

    [QueryParam]
    public DateTimeOffset? CreatedBefore { get; init; }
}

public class ApprovalByIdRequest
{
    [RouteParam]
    [BindFrom("approvalId")]
    public Guid ApprovalId { get; init; }
}

public sealed class UpdateApprovalMetadataRequest : ApprovalByIdRequest
{
    public string? Assignee { get; init; }

    public string? Reason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? Actor { get; init; }

    public string? Comment { get; init; }
}

public sealed class ApprovalCommentRequest : ApprovalByIdRequest
{
    public string? Actor { get; init; }

    public string Comment { get; init; } = string.Empty;
}

public sealed class ApprovalDecisionRequest : ApprovalByIdRequest
{
    public string? Actor { get; init; }

    public string? Comment { get; init; }
}

public sealed record AuditEventResponse(
    Guid AuditId,
    string Category,
    string Action,
    Guid? InstanceId,
    string? WorkflowName,
    string? StepId,
    string Actor,
    JsonObject Details,
    DateTimeOffset CreatedAt)
{
    public static AuditEventResponse FromModel(AuditEventView model) => new(
        model.AuditId,
        model.Category,
        model.Action,
        model.InstanceId,
        model.WorkflowName,
        model.StepId,
        model.Actor,
        model.Details,
        model.CreatedAt);
}

public sealed class ListAuditEventsRequest
{
    [QueryParam]
    public int? Take { get; init; }

    [QueryParam]
    public Guid? InstanceId { get; init; }

    [QueryParam]
    public string? WorkflowName { get; init; }

    [QueryParam]
    public string? Category { get; init; }

    [QueryParam]
    public string? Action { get; init; }

    [QueryParam]
    public string? Actor { get; init; }

    [QueryParam]
    public DateTimeOffset? CreatedAfter { get; init; }

    [QueryParam]
    public DateTimeOffset? CreatedBefore { get; init; }
}
