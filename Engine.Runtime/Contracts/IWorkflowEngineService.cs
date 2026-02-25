using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Domain;

namespace Engine.Runtime.Contracts;

public interface IWorkflowEngineService
{
    Task RegisterWorkflowDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowDefinitionMetadata>> ListWorkflowDefinitionsAsync(CancellationToken cancellationToken);

    Task<WorkflowInstanceChecklistView> StartWorkflowAsync(
        string workflowName,
        JsonObject inputs,
        int? version,
        CancellationToken cancellationToken);

    Task<WorkflowInstanceChecklistView?> GetInstanceChecklistAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<bool> CancelInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<bool> RetryStepAsync(Guid instanceId, string stepId, CancellationToken cancellationToken);

    Task<EventIngestResult> IngestEventAsync(ExternalEventEnvelope externalEvent, CancellationToken cancellationToken);
}
