using Engine.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Activities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEngineActivities(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ActivityRunnerOptions>(configuration.GetSection("Activities"));

        services.AddScoped<LocalActivityRunner>();
        services.AddScoped<ScriptActivityRunner>();
        services.AddScoped<IActivityRunner, RoutedActivityRunner>();

        return services;
    }
}
