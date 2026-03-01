using Engine.Core.Definitions;
using Engine.Core.Domain;

namespace Engine.Core.Abstractions;

public interface IWorkflowRepository
{
    Task<WorkflowDefinitionMetadata> RegisterDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    Task<WorkflowDefinition?> GetDefinitionAsync(string workflowName, int? version, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowDefinitionMetadata>> ListDefinitionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowDraftSummary>> ListDraftsAsync(CancellationToken cancellationToken);

    Task<WorkflowDraftRecord?> GetDraftAsync(Guid draftId, CancellationToken cancellationToken);

    Task<WorkflowDraftSummary> SaveDraftAsync(Guid? draftId, WorkflowDefinition definition, CancellationToken cancellationToken);

    Task<bool> DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken);
}
