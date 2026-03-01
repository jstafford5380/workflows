using Engine.Core.Domain;

namespace Engine.Api.Drafts;

public interface IDraftWorkflowPublisher
{
    Task<WorkflowDefinitionMetadata> PublishAsync(Guid draftId, CancellationToken cancellationToken);
}
