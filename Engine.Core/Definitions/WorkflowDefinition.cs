using System.Text.Json.Nodes;

namespace Engine.Core.Definitions;

public sealed record WorkflowDefinition
{
    public required string Name { get; init; }

    public required int Version { get; init; }

    public string? Description { get; init; }

    public string? Details { get; init; }

    public required IReadOnlyList<WorkflowStepDefinition> Steps { get; init; }
}

public sealed record WorkflowStepDefinition
{
    public required string StepId { get; init; }

    public required string DisplayName { get; init; }

    public string ActivityRef { get; init; } = string.Empty;

    public Dictionary<string, WorkflowInputValue> Inputs { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> OutputsSchema { get; init; } = [];

    public RetryPolicyDefinition RetryPolicy { get; init; } = RetryPolicyDefinition.Default;

    public int? TimeoutSeconds { get; init; }

    public WaitForEventDefinition? WaitForEvent { get; init; }

    public IReadOnlyList<ScriptParameterDefinition> ScriptParameters { get; init; } = [];

    public bool AbortOnFail { get; init; } = true;

    public Dictionary<string, bool> SafetyMetadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ScriptParameterDefinition
{
    public required string Name { get; init; }

    public bool Required { get; init; } = true;
}

public sealed record RetryPolicyDefinition
{
    public static RetryPolicyDefinition Default { get; } = new();

    public int MaxAttempts { get; init; } = 3;

    public int InitialDelaySeconds { get; init; } = 5;

    public int MaxDelaySeconds { get; init; } = 300;

    public double BackoffFactor { get; init; } = 2.0;
}

public sealed record WaitForEventDefinition
{
    public required string EventType { get; init; }

    public required string CorrelationKeyExpression { get; init; }
}

public sealed record WorkflowInputValue
{
    public string? Binding { get; init; }

    public JsonNode? Literal { get; init; }

    public bool IsBinding => !string.IsNullOrWhiteSpace(Binding);

    public static WorkflowInputValue FromBinding(string binding)
    {
        return new WorkflowInputValue { Binding = binding };
    }

    public static WorkflowInputValue FromLiteral(JsonNode? literal)
    {
        return new WorkflowInputValue { Literal = literal?.DeepClone() };
    }
}
