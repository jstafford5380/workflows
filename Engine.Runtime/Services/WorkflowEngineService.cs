using System.Text.Json.Nodes;
using Engine.Core.Abstractions;
using Engine.Core.Definitions;
using Engine.Core.Domain;
using Engine.Core.Execution;
using Engine.Core.Validation;
using Engine.Runtime.Contracts;
using Engine.Runtime.Workers;

namespace Engine.Runtime.Services;

public sealed class WorkflowEngineService : IWorkflowEngineService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IInstanceRepository _instanceRepository;
    private readonly IClock _clock;

    public WorkflowEngineService(
        IWorkflowRepository workflowRepository,
        IInstanceRepository instanceRepository,
        IClock clock)
    {
        _workflowRepository = workflowRepository;
        _instanceRepository = instanceRepository;
        _clock = clock;
    }

    public async Task<WorkflowDefinitionMetadata> RegisterWorkflowDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var validation = WorkflowDefinitionValidator.Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid workflow definition: {string.Join("; ", validation.Errors)}");
        }

        return await _workflowRepository.RegisterDefinitionAsync(definition, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowDefinitionMetadata>> ListWorkflowDefinitionsAsync(CancellationToken cancellationToken)
    {
        return _workflowRepository.ListDefinitionsAsync(cancellationToken);
    }

    public Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string workflowName, int? version, CancellationToken cancellationToken)
    {
        return _workflowRepository.GetDefinitionAsync(workflowName, version, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowDraftSummary>> ListWorkflowDraftsAsync(CancellationToken cancellationToken)
    {
        return _workflowRepository.ListDraftsAsync(cancellationToken);
    }

    public Task<WorkflowDraftRecord?> GetWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        return _workflowRepository.GetDraftAsync(draftId, cancellationToken);
    }

    public async Task<WorkflowDraftSummary> SaveWorkflowDraftAsync(
        Guid? draftId,
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var validation = WorkflowDefinitionValidator.Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid workflow draft: {string.Join("; ", validation.Errors)}");
        }

        return await _workflowRepository.SaveDraftAsync(draftId, definition, cancellationToken);
    }

    public Task<bool> DeleteWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        return _workflowRepository.DeleteDraftAsync(draftId, cancellationToken);
    }

    public async Task<WorkflowDefinitionMetadata> PublishWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await _workflowRepository.GetDraftAsync(draftId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow draft '{draftId}' was not found.");

        var metadata = await RegisterWorkflowDefinitionAsync(draft.Definition, cancellationToken);
        await AppendAuditEventAsync(
            "workflow",
            "published",
            null,
            metadata.Name,
            null,
            "system",
            new JsonObject
            {
                ["name"] = metadata.Name,
                ["version"] = metadata.Version,
                ["revision"] = metadata.Revision,
                ["draftId"] = draftId.ToString("D")
            },
            cancellationToken);
        return metadata;
    }

    public async Task<WorkflowInstanceChecklistView> StartWorkflowAsync(
        string workflowName,
        JsonObject inputs,
        int? version,
        CancellationToken cancellationToken)
    {
        var definition = await _workflowRepository.GetDefinitionAsync(workflowName, version, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow '{workflowName}' was not found.");

        var validation = WorkflowDefinitionValidator.Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Workflow '{workflowName}' failed validation: {string.Join("; ", validation.Errors)}");
        }

        var graph = DependencyGraphBuilder.Build(definition);
        if (!graph.IsValid)
        {
            throw new InvalidOperationException($"Workflow '{workflowName}' has invalid dependencies: {string.Join("; ", graph.Errors)}");
        }

        var normalizedInputs = WorkflowInputRuntimeValidator.ApplyDefaults(definition.InputSchema, inputs);
        var inputValidation = WorkflowInputRuntimeValidator.Validate(definition.InputSchema, normalizedInputs);
        if (!inputValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Workflow inputs failed schema validation: {string.Join("; ", inputValidation.Errors)}");
        }

        var policyValidation = WorkflowPolicyRuntimeValidator.ValidateForStart(definition, normalizedInputs);
        if (!policyValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Workflow policy validation failed: {string.Join("; ", policyValidation.Errors)}");
        }

        var now = _clock.UtcNow;
        var instanceId = Guid.NewGuid();

        var stepById = definition.Steps.ToDictionary(x => x.StepId, StringComparer.OrdinalIgnoreCase);
        var orderedStepIds = graph.TopologicalOrder;

        var steps = new List<StepRunRecord>();
        var dependencies = new List<StepDependencyRecord>();
        var outbox = new List<OutboxMessageRecord>();

        for (var i = 0; i < orderedStepIds.Count; i++)
        {
            var stepId = orderedStepIds[i];
            var stepDefinition = stepById[stepId];
            var hasDependencies = graph.Dependencies[stepId].Count > 0;
            var status = hasDependencies ? StepRunStatus.Pending : StepRunStatus.Runnable;

            var stepRun = new StepRunRecord(
                instanceId,
                stepDefinition.StepId,
                stepDefinition.DisplayName,
                stepDefinition.ActivityRef,
                status,
                0,
                i,
                $"{instanceId}:{stepDefinition.StepId}",
                null,
                null,
                hasDependencies ? null : now,
                null,
                null,
                null,
                new JsonObject(),
                stepDefinition);

            steps.Add(stepRun);

            foreach (var dependsOnStepId in graph.Dependencies[stepId])
            {
                dependencies.Add(new StepDependencyRecord(instanceId, stepId, dependsOnStepId));
            }

            if (!hasDependencies)
            {
                outbox.Add(CreateEnqueueOutbox(instanceId, stepId, now));
            }
        }

        var instance = new WorkflowInstanceRecord(
            instanceId,
            definition.Name,
            definition.Version,
            WorkflowInstanceStatus.Running,
            normalizedInputs,
            now,
            now);

        await _instanceRepository.CreateInstanceAsync(instance, steps, dependencies, outbox, cancellationToken);
        await AppendAuditEventAsync(
            "run",
            "started",
            instanceId,
            definition.Name,
            null,
            "system",
            new JsonObject
            {
                ["workflowName"] = definition.Name,
                ["workflowVersion"] = definition.Version
            },
            cancellationToken);

        return (await GetInstanceChecklistAsync(instanceId, cancellationToken))
            ?? throw new InvalidOperationException("Failed to load newly created workflow instance.");
    }

    public async Task<WorkflowInstanceChecklistView?> GetInstanceChecklistAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetInstanceAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        var steps = await _instanceRepository.GetStepRunsAsync(instanceId, cancellationToken);
        var dependencies = await _instanceRepository.GetDependenciesAsync(instanceId, cancellationToken);

        var statusByStep = steps.ToDictionary(x => x.StepId, x => x.Status, StringComparer.OrdinalIgnoreCase);
        var blockedByMap = dependencies
            .GroupBy(x => x.StepId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Where(d => !IsSatisfied(statusByStep, d.DependsOnStepId)).Select(d => d.DependsOnStepId).ToList(),
                StringComparer.OrdinalIgnoreCase);
        var dependsOnMap = dependencies
            .GroupBy(x => x.StepId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => d.DependsOnStepId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var checklistSteps = steps
            .OrderBy(x => x.StepOrder)
            .Select(step => new ChecklistStepView(
                step.StepId,
                step.DisplayName,
                step.Status,
                step.Attempt,
                step.StartedAt,
                step.FinishedAt,
                dependsOnMap.TryGetValue(step.StepId, out var dependsOn) ? dependsOn : [],
                blockedByMap.TryGetValue(step.StepId, out var blockedBy) ? blockedBy : [],
                step.LastError,
                step.Outputs.Select(kvp => kvp.Key).ToList(),
                step.StepDefinition.SafetyMetadata))
            .ToList();

        return new WorkflowInstanceChecklistView(
            instance.InstanceId,
            instance.WorkflowName,
            instance.WorkflowVersion,
            instance.Status,
            instance.CreatedAt,
            instance.UpdatedAt,
            instance.Inputs,
            checklistSteps);
    }

    public async Task<IReadOnlyList<WorkflowInstanceSummaryView>> ListInstancesAsync(CancellationToken cancellationToken)
    {
        var instances = await _instanceRepository.ListInstancesAsync(200, cancellationToken);
        return instances
            .Select(x => new WorkflowInstanceSummaryView(
                x.InstanceId,
                x.WorkflowName,
                x.WorkflowVersion,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();
    }

    public Task<bool> CancelInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        return CancelInstanceWithAuditAsync(instanceId, cancellationToken);
    }

    public async Task<IReadOnlyList<StepExecutionLogView>> GetStepExecutionLogsAsync(Guid instanceId, string stepId, CancellationToken cancellationToken)
    {
        var logs = await _instanceRepository.GetStepExecutionLogsAsync(instanceId, stepId, _clock.UtcNow, cancellationToken);
        return logs
            .Select(x => new StepExecutionLogView(x.Attempt, x.IsSuccess, x.ConsoleOutput, x.CreatedAt))
            .ToList();
    }

    public Task<EventIngestResult> IngestEventAsync(ExternalEventEnvelope externalEvent, CancellationToken cancellationToken)
    {
        return _instanceRepository.IngestExternalEventAsync(externalEvent, _clock.UtcNow, cancellationToken);
    }

    public Task<bool> RetryStepAsync(Guid instanceId, string stepId, CancellationToken cancellationToken)
    {
        return RetryStepWithAuditAsync(instanceId, stepId, cancellationToken);
    }

    public Task<IReadOnlyList<ApprovalRequestView>> ListApprovalsAsync(
        ApprovalRequestStatus? status,
        Guid? instanceId,
        string? workflowName,
        string? assignee,
        string? stepId,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        CancellationToken cancellationToken)
    {
        return _instanceRepository.ListApprovalRequestsAsync(
            status,
            instanceId,
            workflowName,
            assignee,
            stepId,
            createdAfter,
            createdBefore,
            _clock.UtcNow,
            cancellationToken);
    }

    public Task<ApprovalRequestView?> GetApprovalAsync(Guid approvalId, CancellationToken cancellationToken)
    {
        return _instanceRepository.GetApprovalRequestAsync(approvalId, _clock.UtcNow, cancellationToken);
    }

    public async Task<ApprovalRequestView?> UpdateApprovalMetadataAsync(
        Guid approvalId,
        string? assignee,
        string? reason,
        DateTimeOffset? expiresAt,
        string? actor,
        string? comment,
        CancellationToken cancellationToken)
    {
        var normalizedActor = NormalizeActor(actor);
        var commentRecord = string.IsNullOrWhiteSpace(comment)
            ? null
            : new ApprovalCommentRecord(normalizedActor, comment.Trim(), _clock.UtcNow);

        var updated = await _instanceRepository.UpdateApprovalMetadataAsync(
            approvalId,
            assignee,
            reason,
            expiresAt,
            commentRecord,
            _clock.UtcNow,
            cancellationToken);

        if (updated is null)
        {
            return null;
        }

        await AppendAuditEventAsync(
            "approval",
            "metadata.updated",
            updated.InstanceId,
            updated.WorkflowName,
            updated.StepId,
            normalizedActor,
            new JsonObject
            {
                ["approvalId"] = updated.ApprovalId.ToString("D"),
                ["assignee"] = updated.Assignee,
                ["reason"] = updated.Reason,
                ["expiresAt"] = updated.ExpiresAt
            },
            cancellationToken);
        return updated;
    }

    public async Task<ApprovalRequestView?> AddApprovalCommentAsync(
        Guid approvalId,
        string actor,
        string comment,
        CancellationToken cancellationToken)
    {
        var normalizedActor = NormalizeActor(actor);
        var updated = await _instanceRepository.AddApprovalCommentAsync(
            approvalId,
            new ApprovalCommentRecord(normalizedActor, comment.Trim(), _clock.UtcNow),
            _clock.UtcNow,
            cancellationToken);

        if (updated is null)
        {
            return null;
        }

        await AppendAuditEventAsync(
            "approval",
            "comment.added",
            updated.InstanceId,
            updated.WorkflowName,
            updated.StepId,
            normalizedActor,
            new JsonObject
            {
                ["approvalId"] = updated.ApprovalId.ToString("D"),
                ["comment"] = comment.Trim()
            },
            cancellationToken);
        return updated;
    }

    public async Task<ApprovalRequestView?> ResolveApprovalAsync(
        Guid approvalId,
        bool approved,
        string? actor,
        string? comment,
        CancellationToken cancellationToken)
    {
        var normalizedActor = NormalizeActor(actor);
        var commentRecord = string.IsNullOrWhiteSpace(comment)
            ? null
            : new ApprovalCommentRecord(normalizedActor, comment.Trim(), _clock.UtcNow);

        var resolution = await _instanceRepository.ResolveApprovalAsync(
            approvalId,
            approved,
            commentRecord,
            _clock.UtcNow,
            cancellationToken);

        if (resolution.Approval is null)
        {
            return null;
        }

        if (resolution.Applied && resolution.ResolutionEvent is not null)
        {
            await _instanceRepository.IngestExternalEventAsync(resolution.ResolutionEvent, _clock.UtcNow, cancellationToken);
        }

        await AppendAuditEventAsync(
            "approval",
            approved ? "approved" : "rejected",
            resolution.Approval.InstanceId,
            resolution.Approval.WorkflowName,
            resolution.Approval.StepId,
            normalizedActor,
            new JsonObject
            {
                ["approvalId"] = resolution.Approval.ApprovalId.ToString("D"),
                ["comment"] = commentRecord?.Comment
            },
            cancellationToken);

        return await _instanceRepository.GetApprovalRequestAsync(approvalId, _clock.UtcNow, cancellationToken);
    }

    public Task<IReadOnlyList<AuditEventView>> ListAuditEventsAsync(
        int take,
        Guid? instanceId,
        string? workflowName,
        string? category,
        string? action,
        string? actor,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        CancellationToken cancellationToken)
    {
        return _instanceRepository.ListAuditEventsAsync(
            take,
            instanceId,
            workflowName,
            category,
            action,
            actor,
            createdAfter,
            createdBefore,
            cancellationToken);
    }

    internal static OutboxMessageRecord CreateEnqueueOutbox(Guid instanceId, string stepId, DateTimeOffset availableAt)
    {
        var payload = new WorkItemPayload(instanceId, stepId, availableAt).ToJson();
        return new OutboxMessageRecord(
            Guid.NewGuid(),
            OutboxMessageType.EnqueueWorkItem,
            payload,
            DateTimeOffset.UtcNow,
            null);
    }

    private async Task<bool> CancelInstanceWithAuditAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var canceled = await _instanceRepository.TryCancelInstanceAsync(instanceId, _clock.UtcNow, cancellationToken);
        if (canceled)
        {
            await AppendAuditEventAsync(
                "run",
                "canceled",
                instanceId,
                null,
                null,
                "system",
                new JsonObject(),
                cancellationToken);
        }

        return canceled;
    }

    private async Task<bool> RetryStepWithAuditAsync(Guid instanceId, string stepId, CancellationToken cancellationToken)
    {
        var outboxMessage = CreateEnqueueOutbox(instanceId, stepId, _clock.UtcNow);
        var queued = await _instanceRepository.RetryStepAsync(instanceId, stepId, _clock.UtcNow, outboxMessage, cancellationToken);
        if (queued)
        {
            await AppendAuditEventAsync(
                "run",
                "step.retry.requested",
                instanceId,
                null,
                stepId,
                "system",
                new JsonObject
                {
                    ["stepId"] = stepId
                },
                cancellationToken);
        }

        return queued;
    }

    private Task AppendAuditEventAsync(
        string category,
        string action,
        Guid? instanceId,
        string? workflowName,
        string? stepId,
        string actor,
        JsonObject details,
        CancellationToken cancellationToken)
    {
        return _instanceRepository.AppendAuditEventAsync(
            new AuditEventView(
                Guid.NewGuid(),
                category,
                action,
                instanceId,
                workflowName,
                stepId,
                actor,
                details,
                _clock.UtcNow),
            cancellationToken);
    }

    private static string NormalizeActor(string? actor)
    {
        var trimmed = actor?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "system" : trimmed;
    }

    private static bool IsSatisfied(IReadOnlyDictionary<string, StepRunStatus> statusByStep, string dependsOnStepId)
    {
        return statusByStep.TryGetValue(dependsOnStepId, out var status) && status == StepRunStatus.Succeeded;
    }
}
