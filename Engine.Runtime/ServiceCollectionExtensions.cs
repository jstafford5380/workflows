using Engine.Core.Abstractions;
using Engine.Runtime.Contracts;
using Engine.Runtime.Services;
using Engine.Runtime.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEngineRuntime(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IWorkflowEngineService, WorkflowEngineService>();
        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<WorkflowWorker>();
        return services;
    }
}
