using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine.Persistence.Infrastructure;

internal static class PersistenceJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    public static T Deserialize<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, SerializerOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Failed to deserialize persisted JSON payload.");
        }

        return value;
    }

    public static JsonObject DeserializeObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        var node = JsonNode.Parse(json);
        if (node is JsonObject obj)
        {
            return obj;
        }

        throw new InvalidOperationException("Expected a JSON object payload.");
    }

    public static string SerializeObject(JsonObject obj)
    {
        return obj.ToJsonString(SerializerOptions);
    }
}
