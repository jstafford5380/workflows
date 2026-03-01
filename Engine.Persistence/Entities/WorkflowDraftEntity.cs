namespace Engine.Persistence.Entities;

public sealed class WorkflowDraftEntity
{
    public Guid DraftId { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; }

    public required string DefinitionJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
