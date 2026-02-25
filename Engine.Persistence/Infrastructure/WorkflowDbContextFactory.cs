using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Engine.Persistence.Infrastructure;

public sealed class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("WORKFLOW_ENGINE_DB")
            ?? "Host=localhost;Port=5432;Database=workflow_engine;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new WorkflowDbContext(optionsBuilder.Options);
    }
}
