using Engine.Core.Definitions;
using Engine.Core.Execution;

namespace Engine.Core.Validation;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, []);
}

public static class WorkflowDefinitionValidator
{
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add("Workflow name is required.");
        }

        if (definition.Version <= 0)
        {
            errors.Add("Workflow version must be greater than zero.");
        }

        if (definition.Steps.Count == 0)
        {
            errors.Add("Workflow must include at least one step.");
            return new ValidationResult(false, errors);
        }

        var allowedInputTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "string",
            "number",
            "boolean",
            "object",
            "array"
        };
        var seenInputFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.InputSchema.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                errors.Add("InputSchema field name is required.");
                continue;
            }

            if (!seenInputFields.Add(field.Name))
            {
                errors.Add($"InputSchema has duplicate field '{field.Name}'.");
            }

            if (string.IsNullOrWhiteSpace(field.Type) || !allowedInputTypes.Contains(field.Type))
            {
                errors.Add(
                    $"InputSchema field '{field.Name}' has unsupported type '{field.Type}'. Allowed: string, number, boolean, object, array.");
            }

            if (field.IsSecret && !field.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"InputSchema field '{field.Name}' can only set isSecret=true when type is 'string'.");
            }

            if (field.Options.Count > 0)
            {
                if (!field.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"InputSchema field '{field.Name}' options are only supported for type 'string'.");
                }

                var seenOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var option in field.Options)
                {
                    if (string.IsNullOrWhiteSpace(option))
                    {
                        errors.Add($"InputSchema field '{field.Name}' has an empty option value.");
                        continue;
                    }

                    if (!seenOptions.Add(option))
                    {
                        errors.Add($"InputSchema field '{field.Name}' has duplicate option '{option}'.");
                    }
                }
            }

            if (field.DefaultValue is not null)
            {
                if (!MatchesSchemaType(field.DefaultValue, field.Type))
                {
                    errors.Add($"InputSchema field '{field.Name}' defaultValue does not match type '{field.Type}'.");
                }
                else if (field.Options.Count > 0
                         && field.DefaultValue.GetValueKind() == System.Text.Json.JsonValueKind.String
                         && !field.Options.Contains(field.DefaultValue.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"InputSchema field '{field.Name}' defaultValue '{field.DefaultValue}' must be one of the declared options.");
                }
            }
        }

        var policy = definition.Policy ?? WorkflowPolicyDefinition.Empty;
        var seenRiskLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var riskLabel in policy.RiskLabels)
        {
            if (string.IsNullOrWhiteSpace(riskLabel))
            {
                errors.Add("Policy risk labels cannot contain empty values.");
                continue;
            }

            if (!seenRiskLabels.Add(riskLabel.Trim()))
            {
                errors.Add($"Policy has duplicate risk label '{riskLabel}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(policy.EnvironmentInputKey))
        {
            errors.Add("Policy EnvironmentInputKey is required.");
        }

        if (string.IsNullOrWhiteSpace(policy.TicketInputKey))
        {
            errors.Add("Policy TicketInputKey is required.");
        }

        if (policy.ProductionValues.Count == 0 || policy.ProductionValues.All(string.IsNullOrWhiteSpace))
        {
            errors.Add("Policy ProductionValues must include at least one non-empty value.");
        }

        if (policy.TicketRequired
            && !definition.InputSchema.Fields.Any(f => f.Name.Equals(policy.TicketInputKey, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(
                $"Policy requires ticket input '{policy.TicketInputKey}', but this key is not declared in InputSchema.");
        }

        var seenStepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in definition.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.StepId))
            {
                errors.Add("StepId is required for each step.");
                continue;
            }

            if (!seenStepIds.Add(step.StepId))
            {
                errors.Add($"Duplicate step id '{step.StepId}'.");
            }

            if (string.IsNullOrWhiteSpace(step.DisplayName))
            {
                errors.Add($"Step '{step.StepId}' must include DisplayName.");
            }

            if (step.WaitForEvent is null && string.IsNullOrWhiteSpace(step.ActivityRef))
            {
                errors.Add($"Step '{step.StepId}' must include ActivityRef when WaitForEvent is not configured.");
            }

            if (step.RetryPolicy.MaxAttempts <= 0)
            {
                errors.Add($"Step '{step.StepId}' has invalid RetryPolicy.MaxAttempts.");
            }

            if (step.RetryPolicy.InitialDelaySeconds <= 0 || step.RetryPolicy.MaxDelaySeconds <= 0)
            {
                errors.Add($"Step '{step.StepId}' has invalid retry delay settings.");
            }

            if (step.RetryPolicy.BackoffFactor < 1)
            {
                errors.Add($"Step '{step.StepId}' BackoffFactor must be >= 1.");
            }

            if (step.TimeoutSeconds is <= 0)
            {
                errors.Add($"Step '{step.StepId}' TimeoutSeconds must be > 0 when specified.");
            }

            if (step.WaitForEvent is not null)
            {
                if (string.IsNullOrWhiteSpace(step.WaitForEvent.EventType))
                {
                    errors.Add($"Step '{step.StepId}' WaitForEvent.EventType is required.");
                }

                if (string.IsNullOrWhiteSpace(step.WaitForEvent.CorrelationKeyExpression))
                {
                    errors.Add($"Step '{step.StepId}' WaitForEvent.CorrelationKeyExpression is required.");
                }
                else if (!BindingReference.TryParse(step.WaitForEvent.CorrelationKeyExpression, out var correlationBinding, out var correlationError))
                {
                    errors.Add($"Step '{step.StepId}' WaitForEvent correlation key is invalid: {correlationError}");
                }
                else if (correlationBinding!.Source == BindingSource.StepOutput)
                {
                    errors.Add($"Step '{step.StepId}' WaitForEvent correlation key cannot depend on step outputs in v1.");
                }

                if (step.ScriptParameters.Count > 0)
                {
                    errors.Add($"Step '{step.StepId}' cannot define ScriptParameters when WaitForEvent is configured.");
                }
            }

            if (step.ScriptParameters.Count > 0)
            {
                var seenParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var parameter in step.ScriptParameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter.Name))
                    {
                        errors.Add($"Step '{step.StepId}' has a script parameter with no name.");
                        continue;
                    }

                    if (!seenParameters.Add(parameter.Name))
                    {
                        errors.Add($"Step '{step.StepId}' has duplicate script parameter '{parameter.Name}'.");
                    }

                    if (!step.Inputs.ContainsKey(parameter.Name))
                    {
                        errors.Add($"Step '{step.StepId}' script parameter '{parameter.Name}' must reference a matching key in Inputs.");
                    }
                }
            }

            foreach (var (inputName, inputValue) in step.Inputs)
            {
                if (!inputValue.IsBinding)
                {
                    continue;
                }

                if (!BindingReference.TryParse(inputValue.Binding!, out _, out var bindingError))
                {
                    errors.Add($"Step '{step.StepId}' input '{inputName}' binding is invalid: {bindingError}");
                }
            }
        }

        if (policy.RequiresApprovalForProd)
        {
            var hasApprovalStep = definition.Steps.Any(
                step => step.WaitForEvent is not null
                        && step.WaitForEvent.EventType.Equals("approval", StringComparison.OrdinalIgnoreCase));
            if (!hasApprovalStep)
            {
                errors.Add("Policy requires approval for production runs, but no approval wait step is configured.");
            }
        }

        var graph = DependencyGraphBuilder.Build(definition);
        errors.AddRange(graph.Errors);

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static bool MatchesSchemaType(System.Text.Json.Nodes.JsonNode value, string fieldType)
    {
        var kind = value.GetValueKind();
        return fieldType.ToLowerInvariant() switch
        {
            "string" => kind == System.Text.Json.JsonValueKind.String,
            "number" => kind == System.Text.Json.JsonValueKind.Number,
            "boolean" => kind == System.Text.Json.JsonValueKind.True || kind == System.Text.Json.JsonValueKind.False,
            "object" => kind == System.Text.Json.JsonValueKind.Object,
            "array" => kind == System.Text.Json.JsonValueKind.Array,
            _ => false
        };
    }
}
