using Engine.Core.Definitions;

namespace Engine.Core.Execution;

public sealed record DependencyGraphResult(
    IReadOnlyDictionary<string, IReadOnlySet<string>> Dependencies,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Dependents,
    IReadOnlyList<string> TopologicalOrder,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class DependencyGraphBuilder
{
    public static DependencyGraphResult Build(WorkflowDefinition definition)
    {
        var errors = new List<string>();
        var stepIds = definition.Steps.Select(s => s.StepId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = definition.Steps.ToDictionary(
            s => s.StepId,
            _ => (IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var step in definition.Steps)
        {
            var stepDependencies = (HashSet<string>)dependencies[step.StepId];
            foreach (var input in step.Inputs.Values.Where(v => v.IsBinding))
            {
                if (!BindingReference.TryParse(input.Binding!, out var binding, out var error))
                {
                    errors.Add($"Step '{step.StepId}' has invalid binding: {error}");
                    continue;
                }

                if (binding!.Source != BindingSource.StepOutput)
                {
                    continue;
                }

                if (!stepIds.Contains(binding.StepId!))
                {
                    errors.Add($"Step '{step.StepId}' references unknown step '{binding.StepId}'.");
                    continue;
                }

                if (string.Equals(step.StepId, binding.StepId, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Step '{step.StepId}' cannot depend on itself.");
                    continue;
                }

                stepDependencies.Add(binding.StepId!);
            }
        }

        var dependents = definition.Steps.ToDictionary(
            s => s.StepId,
            _ => (IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (stepId, deps) in dependencies)
        {
            foreach (var dep in deps)
            {
                ((HashSet<string>)dependents[dep]).Add(stepId);
            }
        }

        var topologicalOrder = TopologicalSort(dependencies, errors);
        return new DependencyGraphResult(dependencies, dependents, topologicalOrder, errors);
    }

    private static IReadOnlyList<string> TopologicalSort(
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencies,
        ICollection<string> errors)
    {
        var inDegree = dependencies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count, StringComparer.OrdinalIgnoreCase);
        var reverse = dependencies.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var (stepId, deps) in dependencies)
        {
            foreach (var dep in deps)
            {
                reverse[dep].Add(stepId);
            }
        }

        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var result = new List<string>();

        while (queue.TryDequeue(out var node))
        {
            result.Add(node);
            foreach (var child in reverse[node])
            {
                inDegree[child]--;
                if (inDegree[child] == 0)
                {
                    queue.Enqueue(child);
                }
            }
        }

        if (result.Count != dependencies.Count)
        {
            errors.Add("Workflow contains at least one dependency cycle.");
        }

        return result;
    }
}
