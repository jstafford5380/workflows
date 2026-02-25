using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Activities;
using Engine.Api.Bundles;
using Engine.Persistence;
using Engine.Runtime;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddFastEndpoints();
builder.Services.AddEnginePersistence(builder.Configuration);
builder.Services.AddEngineActivities(builder.Configuration);
builder.Services.AddEngineRuntime();
builder.Services.Configure<BundleOptions>(builder.Configuration.GetSection("Bundles"));
builder.Services.AddSingleton<IBundleService, BundleService>();

var app = builder.Build();

await EnsureDatabaseAsync(app.Services, app.Configuration);

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = null;
    config.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    config.Serializer.Options.WriteIndented = true;
    config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
});

await app.RunAsync();

static async Task EnsureDatabaseAsync(IServiceProvider services, IConfiguration configuration)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
    var provider = configuration["Persistence:Provider"] ?? "InMemory";

    if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
}
