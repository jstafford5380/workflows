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

    public async Task RegisterDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.WorkflowDefinitions
            .SingleOrDefaultAsync(x => x.Name == definition.Name && x.Version == definition.Version, cancellationToken);

        if (existing is null)
        {
            existing = new WorkflowDefinitionEntity
            {
                Name = definition.Name,
                Version = definition.Version,
                RegisteredAt = DateTimeOffset.UtcNow,
                DefinitionJson = PersistenceJson.Serialize(definition)
            };
            _dbContext.WorkflowDefinitions.Add(existing);
        }
        else
        {
            existing.DefinitionJson = PersistenceJson.Serialize(definition);
            existing.RegisteredAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
            .Select(x => new WorkflowDefinitionMetadata(x.Name, x.Version, x.RegisteredAt))
            .ToListAsync(cancellationToken);

        return items;
    }
}
