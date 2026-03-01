using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Validation;

namespace Engine.Tests.Core;

public sealed class WorkflowInputRuntimeValidatorTests
{
    [Fact]
    public void ApplyDefaults_ShouldPopulateMissingFields()
    {
        var schema = new WorkflowInputSchemaDefinition
        {
            Fields =
            [
                new WorkflowInputFieldDefinition
                {
                    Name = "tier",
                    Type = "string",
                    DefaultValue = JsonValue.Create("standard")
                },
                new WorkflowInputFieldDefinition
                {
                    Name = "enabled",
                    Type = "boolean",
                    DefaultValue = JsonValue.Create(true)
                }
            ]
        };

        var inputs = new JsonObject
        {
            ["name"] = "demo"
        };

        var normalized = WorkflowInputRuntimeValidator.ApplyDefaults(schema, inputs);

        Assert.Equal("demo", normalized["name"]?.ToString());
        Assert.Equal("standard", normalized["tier"]?.ToString());
        Assert.Equal("true", normalized["enabled"]?.ToString());
    }

    [Fact]
    public void Validate_ShouldRejectMissingRequiredWrongTypeAndInvalidOption()
    {
        var schema = new WorkflowInputSchemaDefinition
        {
            Fields =
            [
                new WorkflowInputFieldDefinition
                {
                    Name = "projectName",
                    Type = "string",
                    Required = true
                },
                new WorkflowInputFieldDefinition
                {
                    Name = "instanceCount",
                    Type = "number",
                    Required = true
                },
                new WorkflowInputFieldDefinition
                {
                    Name = "projectTier",
                    Type = "string",
                    Required = true,
                    Options = ["basic", "standard", "premium"]
                }
            ]
        };

        var inputs = new JsonObject
        {
            ["instanceCount"] = "three",
            ["projectTier"] = "enterprise"
        };

        var result = WorkflowInputRuntimeValidator.Validate(schema, inputs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("projectName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, x => x.Contains("instanceCount", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, x => x.Contains("projectTier", StringComparison.OrdinalIgnoreCase));
    }
}
