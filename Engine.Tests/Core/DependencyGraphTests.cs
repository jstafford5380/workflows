using Engine.Core.Definitions;
using Engine.Core.Execution;

namespace Engine.Tests.Core;

public sealed class DependencyGraphTests
{
    [Fact]
    public void Build_ShouldInferDependencies_AndTopologicalOrder()
    {
        var definition = new WorkflowDefinition
        {
            Name = "test",
            Version = 1,
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "A",
                    DisplayName = "A",
                    ActivityRef = "local.echo"
                },
                new WorkflowStepDefinition
                {
                    StepId = "B",
                    DisplayName = "B",
                    ActivityRef = "local.echo",
                    Inputs = new Dictionary<string, WorkflowInputValue>
                    {
                        ["fromA"] = WorkflowInputValue.FromBinding("$.steps.A.outputs.value")
                    }
                },
                new WorkflowStepDefinition
                {
                    StepId = "C",
                    DisplayName = "C",
                    ActivityRef = "local.echo",
                    Inputs = new Dictionary<string, WorkflowInputValue>
                    {
                        ["fromB"] = WorkflowInputValue.FromBinding("$.steps.B.outputs.value")
                    }
                }
            ]
        };

        var graph = DependencyGraphBuilder.Build(definition);

        Assert.True(graph.IsValid);
        Assert.Equal(["A", "B", "C"], graph.TopologicalOrder);
        Assert.Empty(graph.Dependencies["A"]);
        Assert.Equal(["A"], graph.Dependencies["B"].ToArray());
        Assert.Equal(["B"], graph.Dependencies["C"].ToArray());
    }

    [Fact]
    public void Build_ShouldReportCycle()
    {
        var definition = new WorkflowDefinition
        {
            Name = "cycle",
            Version = 1,
            Steps =
            [
                new WorkflowStepDefinition
                {
                    StepId = "A",
                    DisplayName = "A",
                    ActivityRef = "local.echo",
                    Inputs = new Dictionary<string, WorkflowInputValue>
                    {
                        ["fromB"] = WorkflowInputValue.FromBinding("$.steps.B.outputs.value")
                    }
                },
                new WorkflowStepDefinition
                {
                    StepId = "B",
                    DisplayName = "B",
                    ActivityRef = "local.echo",
                    Inputs = new Dictionary<string, WorkflowInputValue>
                    {
                        ["fromA"] = WorkflowInputValue.FromBinding("$.steps.A.outputs.value")
                    }
                }
            ]
        };

        var graph = DependencyGraphBuilder.Build(definition);

        Assert.False(graph.IsValid);
        Assert.Contains(graph.Errors, e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }
}
