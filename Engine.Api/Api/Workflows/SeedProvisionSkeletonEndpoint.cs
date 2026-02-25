using System.Text.Json;
using Engine.Api.Api.Common;
using Engine.Core.Definitions;
using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Workflows;

public sealed class SeedProvisionSkeletonEndpoint : EndpointWithoutRequest<WorkflowVersionResponse>
{
    private readonly IWorkflowEngineService _engine;
    private readonly IWebHostEnvironment _environment;

    public SeedProvisionSkeletonEndpoint(IWorkflowEngineService engine, IWebHostEnvironment environment)
    {
        _engine = engine;
        _environment = environment;
    }

    public override void Configure()
    {
        Post("workflows/seed/provision-skeleton");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Seed the sample workflow";
            s.Description = "Loads and registers the local sample provisioning skeleton workflow file.";
            s.Response<WorkflowVersionResponse>(StatusCodes.Status200OK, "Sample workflow registered.");
            s.Response<ApiErrorResponse>(StatusCodes.Status400BadRequest, "Sample workflow file is invalid.");
            s.Response<SeedWorkflowFileNotFoundResponse>(StatusCodes.Status404NotFound, "Sample workflow file is missing.");
        });

        Description(b => b
            .Produces<WorkflowVersionResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest, "application/json")
            .Produces<SeedWorkflowFileNotFoundResponse>(StatusCodes.Status404NotFound, "application/json"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var path = Path.Combine(_environment.ContentRootPath, "Samples", "provision-skeleton.json");
        if (!File.Exists(path))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(
                new SeedWorkflowFileNotFoundResponse("Seed workflow file not found.", path),
                cancellationToken: ct);
            return;
        }

        await using var stream = File.OpenRead(path);
        var definition = await JsonSerializer.DeserializeAsync<WorkflowDefinition>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            ct);

        if (definition is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse("Seed workflow definition file is invalid."),
                cancellationToken: ct);
            return;
        }

        await _engine.RegisterWorkflowDefinitionAsync(definition, ct);
        await HttpContext.Response.WriteAsJsonAsync(
            new WorkflowVersionResponse(definition.Name, definition.Version),
            cancellationToken: ct);
    }
}
