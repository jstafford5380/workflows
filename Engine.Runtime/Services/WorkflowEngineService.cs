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

    public async Task RegisterWorkflowDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var validation = WorkflowDefinitionValidator.Validate(definition);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid workflow definition: {string.Join("; ", validation.Errors)}");
        }

        await _workflowRepository.RegisterDefinitionAsync(definition, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowDefinitionMetadata>> ListWorkflowDefinitionsAsync(CancellationToken cancellationToken)
    {
        return _workflowRepository.ListDefinitionsAsync(cancellationToken);
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
            inputs.DeepClone()!.AsObject(),
            now,
            now);

        await _instanceRepository.CreateInstanceAsync(instance, steps, dependencies, outbox, cancellationToken);

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

        var checklistSteps = steps
            .OrderBy(x => x.StepOrder)
            .Select(step => new ChecklistStepView(
                step.StepId,
                step.DisplayName,
                step.Status,
                step.Attempt,
                step.StartedAt,
                step.FinishedAt,
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

    public Task<bool> CancelInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        return _instanceRepository.TryCancelInstanceAsync(instanceId, _clock.UtcNow, cancellationToken);
    }

    public Task<EventIngestResult> IngestEventAsync(ExternalEventEnvelope externalEvent, CancellationToken cancellationToken)
    {
        return _instanceRepository.IngestExternalEventAsync(externalEvent, _clock.UtcNow, cancellationToken);
    }

    public Task<bool> RetryStepAsync(Guid instanceId, string stepId, CancellationToken cancellationToken)
    {
        var outboxMessage = CreateEnqueueOutbox(instanceId, stepId, _clock.UtcNow);
        return _instanceRepository.RetryStepAsync(instanceId, stepId, _clock.UtcNow, outboxMessage, cancellationToken);
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

    private static bool IsSatisfied(IReadOnlyDictionary<string, StepRunStatus> statusByStep, string dependsOnStepId)
    {
        return statusByStep.TryGetValue(dependsOnStepId, out var status) && status == StepRunStatus.Succeeded;
    }
}
