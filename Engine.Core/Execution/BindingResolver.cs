using System.Text.Json.Nodes;
using Engine.Core.Definitions;

namespace Engine.Core.Execution;

public static class BindingResolver
{
    public static JsonNode ResolveBinding(
        BindingReference binding,
        Guid instanceId,
        JsonObject workflowInputs,
        IReadOnlyDictionary<string, JsonObject> stepOutputs)
    {
        return binding.Source switch
        {
            BindingSource.InstanceId => JsonValue.Create(instanceId.ToString())!,
            BindingSource.Inputs => ResolveInput(binding.InputKey!, workflowInputs),
            BindingSource.StepOutput => ResolveStepOutput(binding.StepId!, binding.OutputKey!, stepOutputs),
            _ => throw new InvalidOperationException($"Unsupported binding source '{binding.Source}'.")
        };
    }

    public static JsonObject ResolveStepInputs(
        WorkflowStepDefinition step,
        Guid instanceId,
        JsonObject workflowInputs,
        IReadOnlyDictionary<string, JsonObject> stepOutputs)
    {
        var result = new JsonObject();

        foreach (var (inputName, inputValue) in step.Inputs)
        {
            if (!inputValue.IsBinding)
            {
                result[inputName] = inputValue.Literal?.DeepClone();
                continue;
            }

            if (!BindingReference.TryParse(inputValue.Binding!, out var binding, out var error))
            {
                throw new InvalidOperationException(error);
            }

            result[inputName] = ResolveBinding(binding!, instanceId, workflowInputs, stepOutputs);
        }

        return result;
    }

    public static string ResolveCorrelationKey(string expression, Guid instanceId, JsonObject workflowInputs)
    {
        if (!BindingReference.TryParse(expression, out var binding, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (binding!.Source == BindingSource.StepOutput)
        {
            throw new InvalidOperationException("WaitForEvent correlation key cannot reference step outputs in v1.");
        }

        var node = ResolveBinding(binding, instanceId, workflowInputs, new Dictionary<string, JsonObject>());
        return node.ToString();
    }

    private static JsonNode ResolveInput(string key, JsonObject workflowInputs)
    {
        if (!workflowInputs.TryGetPropertyValue(key, out var node))
        {
            throw new InvalidOperationException($"Workflow input '{key}' was not provided.");
        }

        return node?.DeepClone() ?? JsonValue.Create((string?)null)!;
    }

    private static JsonNode ResolveStepOutput(string stepId, string outputKey, IReadOnlyDictionary<string, JsonObject> stepOutputs)
    {
        if (!stepOutputs.TryGetValue(stepId, out var outputs))
        {
            throw new InvalidOperationException($"Referenced step '{stepId}' has no outputs.");
        }

        if (!outputs.TryGetPropertyValue(outputKey, out var value))
        {
            throw new InvalidOperationException($"Referenced output '{stepId}.{outputKey}' is missing.");
        }

        return value?.DeepClone() ?? JsonValue.Create((string?)null)!;
    }
}
