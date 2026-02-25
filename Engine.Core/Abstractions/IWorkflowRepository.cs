using Engine.Core.Definitions;
using Engine.Core.Domain;

namespace Engine.Core.Abstractions;

public interface IWorkflowRepository
{
    Task RegisterDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    Task<WorkflowDefinition?> GetDefinitionAsync(string workflowName, int? version, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowDefinitionMetadata>> ListDefinitionsAsync(CancellationToken cancellationToken);
}
