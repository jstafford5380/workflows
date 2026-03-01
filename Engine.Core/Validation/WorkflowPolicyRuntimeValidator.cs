using System.Text.Json.Nodes;
using Engine.Core.Definitions;

namespace Engine.Core.Validation;

public static class WorkflowPolicyRuntimeValidator
{
    public static ValidationResult ValidateForStart(WorkflowDefinition definition, JsonObject inputs)
    {
        var errors = new List<string>();
        var policy = definition.Policy ?? WorkflowPolicyDefinition.Empty;

        if (policy.TicketRequired)
        {
            var ticketValue = GetStringValue(inputs, policy.TicketInputKey);
            if (string.IsNullOrWhiteSpace(ticketValue))
            {
                errors.Add($"Policy requires '{policy.TicketInputKey}' for this workflow.");
            }
        }

        if (policy.RequiresApprovalForProd && IsProductionRun(inputs, policy))
        {
            var hasApprovalStep = definition.Steps.Any(
                step => step.WaitForEvent is not null
                        && step.WaitForEvent.EventType.Equals("approval", StringComparison.OrdinalIgnoreCase));
            if (!hasApprovalStep)
            {
                errors.Add("Production runs require an approval step, but none is configured.");
            }
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(false, errors);
    }

    private static bool IsProductionRun(JsonObject inputs, WorkflowPolicyDefinition policy)
    {
        if (!inputs.TryGetPropertyValue(policy.EnvironmentInputKey, out var environmentNode) || environmentNode is null)
        {
            return false;
        }

        if (environmentNode.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            var environment = environmentNode.GetValue<string>();
            return policy.ProductionValues.Any(
                value => environment.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static string? GetStringValue(JsonObject inputs, string key)
    {
        if (!inputs.TryGetPropertyValue(key, out var node) || node is null)
        {
            return null;
        }

        return node.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? node.GetValue<string>()
            : node.ToJsonString();
    }
}
