using Engine.Core.Abstractions;
using Engine.Core.Definitions;
using Engine.Core.Domain;
using Engine.Persistence.Entities;
using Engine.Persistence.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Engine.Persistence.Repositories;

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly WorkflowDbContext _dbContext;

    public WorkflowRepository(WorkflowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowDefinitionMetadata> RegisterDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existingVersions = await _dbContext.WorkflowDefinitions
            .Where(x => x.Name == definition.Name)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        WorkflowDefinitionEntity entity;
        if (existingVersions.Count == 0)
        {
            if (definition.Version != 1)
            {
                throw new InvalidOperationException(
                    $"Workflow '{definition.Name}' must start at version 1. Received version {definition.Version}.");
            }

            entity = new WorkflowDefinitionEntity
            {
                Name = definition.Name,
                Version = definition.Version,
                Revision = 1,
                RegisteredAt = now,
                DefinitionJson = PersistenceJson.Serialize(definition)
            };
            _dbContext.WorkflowDefinitions.Add(entity);
        }
        else
        {
            var latest = existingVersions[0];
            if (definition.Version == latest.Version)
            {
                entity = latest;
                entity.DefinitionJson = PersistenceJson.Serialize(definition);
                entity.RegisteredAt = now;
                entity.Revision += 1;
            }
            else if (definition.Version == latest.Version + 1)
            {
                entity = new WorkflowDefinitionEntity
                {
                    Name = definition.Name,
                    Version = definition.Version,
                    Revision = 1,
                    RegisteredAt = now,
                    DefinitionJson = PersistenceJson.Serialize(definition)
                };
                _dbContext.WorkflowDefinitions.Add(entity);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Workflow '{definition.Name}' version must be {latest.Version} (replace current) or {latest.Version + 1} (next version). Received version {definition.Version}.");
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToMetadata(entity);
    }

    public async Task<WorkflowDefinition?> GetDefinitionAsync(string workflowName, int? version, CancellationToken cancellationToken)
    {
        WorkflowDefinitionEntity? entity;
        if (version.HasValue)
        {
            entity = await _dbContext.WorkflowDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Name == workflowName && x.Version == version.Value, cancellationToken);
        }
        else
        {
            entity = await _dbContext.WorkflowDefinitions
                .AsNoTracking()
                .Where(x => x.Name == workflowName)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return entity is null ? null : PersistenceJson.Deserialize<WorkflowDefinition>(entity.DefinitionJson);
    }

    public async Task<IReadOnlyList<WorkflowDefinitionMetadata>> ListDefinitionsAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext.WorkflowDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        return items
            .Select(ToMetadata)
            .ToList();
    }

    private static WorkflowDefinitionMetadata ToMetadata(WorkflowDefinitionEntity entity)
    {
        var definition = PersistenceJson.Deserialize<WorkflowDefinition>(entity.DefinitionJson);
        return new WorkflowDefinitionMetadata(
            entity.Name,
            entity.Version,
            entity.Revision,
            entity.RegisteredAt,
            definition.Description,
            definition.Details,
            definition.InputSchema);
    }
}
