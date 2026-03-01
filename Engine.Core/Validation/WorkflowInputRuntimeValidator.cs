using System.Text.Json;
using System.Text.Json.Nodes;
using Engine.Core.Definitions;

namespace Engine.Core.Validation;

public static class WorkflowInputRuntimeValidator
{
    public static JsonObject ApplyDefaults(WorkflowInputSchemaDefinition schema, JsonObject inputs)
    {
        var normalized = inputs.DeepClone()?.AsObject() ?? new JsonObject();
        foreach (var field in schema.Fields)
        {
            if (field.DefaultValue is null)
            {
                continue;
            }

            if (!normalized.TryGetPropertyValue(field.Name, out var existing) || existing is null)
            {
                normalized[field.Name] = field.DefaultValue.DeepClone();
            }
        }

        return normalized;
    }

    public static ValidationResult Validate(WorkflowInputSchemaDefinition schema, JsonObject inputs)
    {
        var errors = new List<string>();

        foreach (var field in schema.Fields)
        {
            var hasValue = inputs.TryGetPropertyValue(field.Name, out var value) && value is not null;
            if (!hasValue)
            {
                if (field.Required)
                {
                    errors.Add($"Input '{field.Name}' is required.");
                }

                continue;
            }

            var kind = value!.GetValueKind();
            if (!MatchesSchemaType(kind, field.Type))
            {
                errors.Add($"Input '{field.Name}' must be of type '{field.Type}'.");
                continue;
            }

            if (field.Options.Count > 0 && kind == JsonValueKind.String)
            {
                var optionValue = value.ToString();
                if (!field.Options.Contains(optionValue, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Input '{field.Name}' must be one of: {string.Join(", ", field.Options)}.");
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static bool MatchesSchemaType(JsonValueKind kind, string fieldType)
    {
        return fieldType.ToLowerInvariant() switch
        {
            "string" => kind == JsonValueKind.String,
            "number" => kind == JsonValueKind.Number,
            "boolean" => kind == JsonValueKind.True || kind == JsonValueKind.False,
            "object" => kind == JsonValueKind.Object,
            "array" => kind == JsonValueKind.Array,
            _ => false
        };
    }
}
