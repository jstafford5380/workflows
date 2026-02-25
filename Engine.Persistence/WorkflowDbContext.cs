using Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Engine.Persistence;

public sealed class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkflowDefinitionEntity> WorkflowDefinitions => Set<WorkflowDefinitionEntity>();

    public DbSet<WorkflowInstanceEntity> WorkflowInstances => Set<WorkflowInstanceEntity>();

    public DbSet<StepRunEntity> StepRuns => Set<StepRunEntity>();

    public DbSet<StepDependencyEntity> StepDependencies => Set<StepDependencyEntity>();

    public DbSet<EventSubscriptionEntity> EventSubscriptions => Set<EventSubscriptionEntity>();

    public DbSet<EventInboxEntity> EventInboxes => Set<EventInboxEntity>();

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<WorkQueueItemEntity> WorkQueueItems => Set<WorkQueueItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkflowDefinitionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.Version }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.DefinitionJson).HasColumnType("text");
        });

        modelBuilder.Entity<WorkflowInstanceEntity>(entity =>
        {
            entity.HasKey(e => e.InstanceId);
            entity.Property(e => e.WorkflowName).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(40);
            entity.Property(e => e.InputsJson).HasColumnType("text");
            entity.HasMany(e => e.StepRuns)
                .WithOne(e => e.WorkflowInstance)
                .HasForeignKey(e => e.InstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StepRunEntity>(entity =>
        {
            entity.HasKey(e => new { e.InstanceId, e.StepId });
            entity.Property(e => e.StepId).HasMaxLength(200);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.ActivityRef).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(40);
            entity.Property(e => e.StepDefinitionJson).HasColumnType("text");
            entity.Property(e => e.OutputsJson).HasColumnType("text");
            entity.Property(e => e.LastError).HasColumnType("text");
            entity.HasIndex(e => new { e.InstanceId, e.Status, e.NextAttemptAt });
        });

        modelBuilder.Entity<StepDependencyEntity>(entity =>
        {
            entity.HasKey(e => new { e.InstanceId, e.StepId, e.DependsOnStepId });
            entity.Property(e => e.StepId).HasMaxLength(200);
            entity.Property(e => e.DependsOnStepId).HasMaxLength(200);
            entity.HasIndex(e => new { e.InstanceId, e.DependsOnStepId });
        });

        modelBuilder.Entity<EventSubscriptionEntity>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId);
            entity.Property(e => e.StepId).HasMaxLength(200);
            entity.Property(e => e.EventType).HasMaxLength(200);
            entity.Property(e => e.CorrelationKey).HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(40);
            entity.Property(e => e.PayloadJson).HasColumnType("text");
            entity.HasIndex(e => new { e.Status, e.EventType, e.CorrelationKey });
        });

        modelBuilder.Entity<EventInboxEntity>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).HasMaxLength(200);
            entity.Property(e => e.EventType).HasMaxLength(200);
            entity.Property(e => e.CorrelationKey).HasMaxLength(300);
            entity.Property(e => e.PayloadHash).HasMaxLength(128);
            entity.Property(e => e.PayloadJson).HasColumnType("text");
            entity.HasIndex(e => new { e.EventType, e.CorrelationKey, e.PayloadHash }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.HasKey(e => e.OutboxId);
            entity.Property(e => e.Type).HasMaxLength(100);
            entity.Property(e => e.PayloadJson).HasColumnType("text");
            entity.HasIndex(e => new { e.ProcessedAt, e.CreatedAt });
        });

        modelBuilder.Entity<WorkQueueItemEntity>(entity =>
        {
            entity.HasKey(e => e.WorkItemId);
            entity.Property(e => e.Kind).HasMaxLength(100);
            entity.Property(e => e.PayloadJson).HasColumnType("text");
            entity.HasIndex(e => new { e.CompletedAt, e.AvailableAt, e.LeaseExpiresAt });
        });
    }
}
