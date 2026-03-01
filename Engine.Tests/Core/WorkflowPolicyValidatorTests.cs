using System.Text.Json.Nodes;
using Engine.Core.Definitions;
using Engine.Core.Validation;

namespace Engine.Tests.Core;

public sealed class WorkflowPolicyValidatorTests
{
    [Fact]
    public void Validate_ShouldRejectRequiresApprovalForProd_WhenApprovalWaitStepMissing()
    {
        var definition = new WorkflowDefinition
        {
            Name = "policy-invalid",
            Version = 1,
            Policy = new WorkflowPolicyDefinition
            {
                RequiresApprovalForProd = true
            },
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "create",
                    DisplayName = "Create",
                    ActivityRef = "local.echo"
                }
            ]
        };

        var result = WorkflowDefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("approval for production", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateForStart_ShouldRequireTicket_WhenPolicyRequiresTicket()
    {
        var definition = new WorkflowDefinition
        {
            Name = "policy-runtime",
            Version = 1,
            Policy = new WorkflowPolicyDefinition
            {
                TicketRequired = true,
                TicketInputKey = "ticketId",
                RequiresApprovalForProd = true,
                EnvironmentInputKey = "environment",
                ProductionValues = ["prod"]
            },
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "approval",
                    DisplayName = "Approval",
                    WaitForEvent = new WaitForEventDefinition
                    {
                        EventType = "approval",
                        CorrelationKeyExpression = "$.inputs.instanceId"
                    }
                }
            ]
        };

        var inputs = new JsonObject
        {
            ["environment"] = "prod",
            ["ticketId"] = ""
        };

        var result = WorkflowPolicyRuntimeValidator.ValidateForStart(definition, inputs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("ticketId", StringComparison.OrdinalIgnoreCase));
    }
}
