using Engine.Core.Definitions;
using Engine.Core.Validation;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Engine.Tests.Core;

public sealed class WorkflowDefinitionValidatorTests
{
    [Fact]
    public void WorkflowDefinition_DefaultsAbortOnFailToTrue_WhenMissingInJson()
    {
        const string json = """
                            {
                              "name": "sample",
                              "version": 1,
                              "steps": [
                                {
                                  "stepId": "A",
                                  "displayName": "Step A",
                                  "activityRef": "local.echo"
                                }
                              ]
                            }
                            """;

        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(definition);
        Assert.True(definition!.Steps[0].AbortOnFail);
    }

    [Fact]
    public void Validate_ShouldRejectUnknownStepBinding()
    {
        var definition = new WorkflowDefinition
        {
            Name = "invalid",
            Version = 1,
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "OnlyStep",
                    DisplayName = "Only Step",
                    ActivityRef = "local.echo",
                    Inputs = new Dictionary<string, WorkflowInputValue>
                    {
                        ["missing"] = WorkflowInputValue.FromBinding("$.steps.NotThere.outputs.value")
                    }
                }
            ]
        };

        var result = WorkflowDefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown step", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldRejectScriptParameterMissingMatchingInput()
    {
        var definition = new WorkflowDefinition
        {
            Name = "invalid-script-params",
            Version = 1,
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "OnlyStep",
                    DisplayName = "Only Step",
                    ActivityRef = "local.echo",
                    Inputs = new Dictionary<string, WorkflowInputValue>
                    {
                        ["projectId"] = WorkflowInputValue.FromLiteral("demo")
                    },
                    ScriptParameters =
                    [
                        new ScriptParameterDefinition
                        {
                            Name = "missingInput",
                            Required = true
                        }
                    ]
                }
            ]
        };

        var result = WorkflowDefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("script parameter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldRejectSecretNonStringAndInvalidOptions()
    {
        var definition = new WorkflowDefinition
        {
            Name = "invalid-input-schema",
            Version = 1,
            InputSchema = new WorkflowInputSchemaDefinition
            {
                Fields =
                [
                    new WorkflowInputFieldDefinition
                    {
                        Name = "count",
                        Type = "number",
                        IsSecret = true
                    },
                    new WorkflowInputFieldDefinition
                    {
                        Name = "tier",
                        Type = "string",
                        Options = ["basic", "basic"],
                        DefaultValue = JsonValue.Create("enterprise")
                    }
                ]
            },
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "StepA",
                    DisplayName = "Step A",
                    ActivityRef = "local.echo"
                }
            ]
        };

        var result = WorkflowDefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("isSecret", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Contains("duplicate option", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Contains("defaultValue", StringComparison.OrdinalIgnoreCase));
    }
}
