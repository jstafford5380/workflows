using Engine.Runtime.Contracts;
using FastEndpoints;

namespace Engine.Api.Api.Events;

public sealed class IngestEventEndpoint : Endpoint<IngestEventRequest, IngestEventResponse>
{
    private readonly IWorkflowEngineService _engine;

    public IngestEventEndpoint(IWorkflowEngineService engine)
    {
        _engine = engine;
    }

    public override void Configure()
    {
        Post("events");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Ingest external event";
            s.Description = "Deduplicates and applies incoming external events to waiting workflow steps.";
            s.RequestParam(r => r.EventId, "Caller supplied event id for deduplication.");
            s.RequestParam(r => r.EventType, "Event type.");
            s.RequestParam(r => r.CorrelationKey, "Correlation key used to match waiting steps.");
            s.RequestParam(r => r.Payload, "Arbitrary event payload object.");
            s.RequestParam(r => r.PayloadHash!, "Optional caller-provided payload hash for dedupe heuristics.");
            s.Response<IngestEventResponse>(StatusCodes.Status200OK, "Event accepted and processed.");
        });

        Description(b => b.Produces<IngestEventResponse>(StatusCodes.Status200OK, "application/json"));
    }

    public override async Task HandleAsync(IngestEventRequest req, CancellationToken ct)
    {
        var result = await _engine.IngestEventAsync(
            new Engine.Core.Domain.ExternalEventEnvelope(
                req.EventId,
                req.EventType,
                req.CorrelationKey,
                req.Payload,
                req.PayloadHash),
            ct);

        await HttpContext.Response.WriteAsJsonAsync(IngestEventResponse.FromModel(result), cancellationToken: ct);
    }
}
