using System.Text.Json.Nodes;
using Engine.Core.Domain;

namespace Engine.Api.Api.Events;

public sealed record IngestEventRequest(
    string EventId,
    string EventType,
    string CorrelationKey,
    JsonObject Payload,
    string? PayloadHash);

public sealed record IngestEventResponse(bool IsDuplicate, int FulfilledSubscriptions)
{
    public static IngestEventResponse FromModel(EventIngestResult model)
    {
        return new IngestEventResponse(model.IsDuplicate, model.FulfilledSubscriptions);
    }
}
