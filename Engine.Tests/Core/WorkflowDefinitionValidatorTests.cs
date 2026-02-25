using Engine.Core.Definitions;
using Engine.Core.Validation;

namespace Engine.Tests.Core;

public sealed class WorkflowDefinitionValidatorTests
{
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
}
