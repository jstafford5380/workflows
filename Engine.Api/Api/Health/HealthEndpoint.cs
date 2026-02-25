using FastEndpoints;

namespace Engine.Api.Api.Health;

public sealed class HealthEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "API health check";
            s.Description = "Returns a simple health payload indicating the API process is up.";
            s.Response<HealthResponse>(StatusCodes.Status200OK, "API is healthy.");
        });

        Description(b => b.Produces<HealthResponse>(StatusCodes.Status200OK));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await HttpContext.Response.WriteAsJsonAsync(new HealthResponse("ok"), cancellationToken: ct);
    }
}
