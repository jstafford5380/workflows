namespace Engine.Core.Execution;

public enum BindingSource
{
    Inputs,
    StepOutput,
    InstanceId
}

public sealed record BindingReference(
    BindingSource Source,
    string? InputKey,
    string? StepId,
    string? OutputKey)
{
    public static bool TryParse(string expression, out BindingReference? reference, out string? error)
    {
        reference = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Binding expression is required.";
            return false;
        }

        if (string.Equals(expression, "$.instanceId", StringComparison.Ordinal))
        {
            reference = new BindingReference(BindingSource.InstanceId, null, null, null);
            return true;
        }

        const string inputsPrefix = "$.inputs.";
        if (expression.StartsWith(inputsPrefix, StringComparison.Ordinal))
        {
            var inputKey = expression[inputsPrefix.Length..];
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                error = $"Binding '{expression}' must include an input key.";
                return false;
            }

            reference = new BindingReference(BindingSource.Inputs, inputKey, null, null);
            return true;
        }

        const string stepsPrefix = "$.steps.";
        const string outputsMarker = ".outputs.";
        if (expression.StartsWith(stepsPrefix, StringComparison.Ordinal))
        {
            var remaining = expression[stepsPrefix.Length..];
            var markerIndex = remaining.IndexOf(outputsMarker, StringComparison.Ordinal);
            if (markerIndex <= 0 || markerIndex + outputsMarker.Length >= remaining.Length)
            {
                error = $"Binding '{expression}' must match $.steps.<stepId>.outputs.<outputKey>.";
                return false;
            }

            var stepId = remaining[..markerIndex];
            var outputKey = remaining[(markerIndex + outputsMarker.Length)..];
            if (string.IsNullOrWhiteSpace(stepId) || string.IsNullOrWhiteSpace(outputKey))
            {
                error = $"Binding '{expression}' has an empty step id or output key.";
                return false;
            }

            reference = new BindingReference(BindingSource.StepOutput, null, stepId, outputKey);
            return true;
        }

        error = $"Unsupported binding expression '{expression}'.";
        return false;
    }
}
