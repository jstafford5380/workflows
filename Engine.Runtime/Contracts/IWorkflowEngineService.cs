using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Domain;

namespace Engine.Runtime.Contracts;

public interface IWorkflowEngineService
{
    Task<WorkflowDefinitionMetadata> RegisterWorkflowDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowDefinitionMetadata>> ListWorkflowDefinitionsAsync(CancellationToken cancellationToken);

    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string workflowName, int? version, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowDraftSummary>> ListWorkflowDraftsAsync(CancellationToken cancellationToken);

    Task<WorkflowDraftRecord?> GetWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken);

    Task<WorkflowDraftSummary> SaveWorkflowDraftAsync(Guid? draftId, WorkflowDefinition definition, CancellationToken cancellationToken);

    Task<bool> DeleteWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken);

    Task<WorkflowDefinitionMetadata> PublishWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken);

    Task<WorkflowInstanceChecklistView> StartWorkflowAsync(
        string workflowName,
        JsonObject inputs,
        int? version,
        CancellationToken cancellationToken);

    Task<WorkflowInstanceChecklistView?> GetInstanceChecklistAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowInstanceSummaryView>> ListInstancesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StepExecutionLogView>> GetStepExecutionLogsAsync(Guid instanceId, string stepId, CancellationToken cancellationToken);

    Task<bool> CancelInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<bool> RetryStepAsync(Guid instanceId, string stepId, CancellationToken cancellationToken);

    Task<EventIngestResult> IngestEventAsync(ExternalEventEnvelope externalEvent, CancellationToken cancellationToken);
}
