namespace Engine.Persistence.Entities;

public sealed class WorkflowDefinitionEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; }

    public required string DefinitionJson { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }
}
