using System.Text.Json.Nodes;

namespace Engine.Runtime.Workers;

public sealed record WorkItemPayload(Guid InstanceId, string StepId, DateTimeOffset AvailableAt)
{
    public static WorkItemPayload FromJson(JsonObject json)
    {
        var instanceIdRaw = json["instanceId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Work item payload missing instanceId.");
        var stepId = json["stepId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Work item payload missing stepId.");

        var availableAtNode = json["availableAt"];
        var availableAt = availableAtNode?.GetValue<DateTimeOffset>() ?? DateTimeOffset.UtcNow;

        return new WorkItemPayload(Guid.Parse(instanceIdRaw), stepId, availableAt);
    }

    public JsonObject ToJson()
    {
        return new JsonObject
        {
            ["instanceId"] = InstanceId.ToString(),
            ["stepId"] = StepId,
            ["availableAt"] = AvailableAt
        };
    }
}
