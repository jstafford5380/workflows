using Engine.Core.Abstractions;
using Engine.Persistence.Queue;
using Engine.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEnginePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Persistence:Provider"] ?? "InMemory";

        services.AddDbContext<WorkflowDbContext>(options =>
        {
            if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
            {
                var cs = configuration.GetConnectionString("WorkflowDb")
                    ?? "Host=localhost;Port=5432;Database=workflow_engine;Username=postgres;Password=postgres";
                options.UseNpgsql(cs);
            }
            else
            {
                options.UseInMemoryDatabase("workflow-engine");
                options.ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                });
            }
        });

        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IInstanceRepository, InstanceRepository>();
        services.AddScoped<IOutbox, DbOutbox>();
        services.AddScoped<IWorkQueue, DbWorkQueue>();

        return services;
    }
}
