using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Execution;

namespace Engine.Tests.Core;

public sealed class BindingResolverTests
{
    [Fact]
    public void ResolveStepInputs_ShouldThrowPolishedMessage_WhenWorkflowInputMissing()
    {
        var step = new WorkflowStepDefinition
        {
            StepId = "CreateProject",
            DisplayName = "Create Project",
            ActivityRef = "bundle://test/create-project.sh",
            Inputs = new Dictionary<string, WorkflowInputValue>
            {
                ["projectId"] = WorkflowInputValue.FromBinding("$.inputs.projectId")
            }
        };

        var ex = Assert.Throws<WorkflowRuntimeValidationException>(() =>
            BindingResolver.ResolveStepInputs(
                step,
                Guid.NewGuid(),
                new JsonObject(),
                new Dictionary<string, JsonObject>()));

        Assert.Contains("Missing required workflow input 'projectId'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveStepInputs_ShouldThrowPolishedMessage_WhenDependentStepOutputMissing()
    {
        var step = new WorkflowStepDefinition
        {
            StepId = "CreateServiceAccounts",
            DisplayName = "Create Service Accounts",
            ActivityRef = "bundle://test/create-service-accounts.sh",
            Inputs = new Dictionary<string, WorkflowInputValue>
            {
                ["projectNumber"] = WorkflowInputValue.FromBinding("$.steps.CreateProject.outputs.projectNumber")
            }
        };

        var ex = Assert.Throws<WorkflowRuntimeValidationException>(() =>
            BindingResolver.ResolveStepInputs(
                step,
                Guid.NewGuid(),
                new JsonObject(),
                new Dictionary<string, JsonObject>()));

        Assert.Contains("Missing output source step 'CreateProject'", ex.Message, StringComparison.Ordinal);
    }
}
