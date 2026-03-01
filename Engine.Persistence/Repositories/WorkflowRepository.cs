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

    public async Task<IReadOnlyList<WorkflowDraftSummary>> ListDraftsAsync(CancellationToken cancellationToken)
    {
        var drafts = await _dbContext.WorkflowDrafts
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new WorkflowDraftSummary(
                x.DraftId,
                x.Name,
                x.Version,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return drafts;
    }

    public async Task<WorkflowDraftRecord?> GetDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WorkflowDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.DraftId == draftId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new WorkflowDraftRecord(
            entity.DraftId,
            PersistenceJson.Deserialize<WorkflowDefinition>(entity.DefinitionJson),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    public async Task<WorkflowDraftSummary> SaveDraftAsync(
        Guid? draftId,
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        WorkflowDraftEntity? entity = null;

        if (draftId.HasValue)
        {
            entity = await _dbContext.WorkflowDrafts
                .SingleOrDefaultAsync(x => x.DraftId == draftId.Value, cancellationToken);
        }

        if (entity is null)
        {
            entity = new WorkflowDraftEntity
            {
                DraftId = draftId ?? Guid.NewGuid(),
                Name = definition.Name,
                Version = definition.Version,
                DefinitionJson = PersistenceJson.Serialize(definition),
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.WorkflowDrafts.Add(entity);
        }
        else
        {
            entity.Name = definition.Name;
            entity.Version = definition.Version;
            entity.DefinitionJson = PersistenceJson.Serialize(definition);
            entity.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new WorkflowDraftSummary(
            entity.DraftId,
            entity.Name,
            entity.Version,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    public async Task<bool> DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WorkflowDrafts
            .SingleOrDefaultAsync(x => x.DraftId == draftId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        _dbContext.WorkflowDrafts.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
