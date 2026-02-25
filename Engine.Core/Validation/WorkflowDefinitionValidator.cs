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

        var graph = DependencyGraphBuilder.Build(definition);
        errors.AddRange(graph.Errors);

        return new ValidationResult(errors.Count == 0, errors);
    }
}
