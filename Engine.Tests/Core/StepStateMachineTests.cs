using Engine.Core.Domain;
using Engine.Core.Execution;

namespace Engine.Tests.Core;

public sealed class StepStateMachineTests
{
    [Theory]
    [InlineData(StepRunStatus.Pending, StepRunStatus.Runnable, true)]
    [InlineData(StepRunStatus.Runnable, StepRunStatus.Running, true)]
    [InlineData(StepRunStatus.Runnable, StepRunStatus.Aborted, true)]
    [InlineData(StepRunStatus.Running, StepRunStatus.Succeeded, true)]
    [InlineData(StepRunStatus.Waiting, StepRunStatus.Aborted, true)]
    [InlineData(StepRunStatus.Waiting, StepRunStatus.Succeeded, true)]
    [InlineData(StepRunStatus.Aborted, StepRunStatus.Runnable, false)]
    [InlineData(StepRunStatus.Succeeded, StepRunStatus.Runnable, false)]
    [InlineData(StepRunStatus.Canceled, StepRunStatus.Runnable, false)]
    public void CanTransition_ShouldReflectRules(StepRunStatus from, StepRunStatus to, bool expected)
    {
        var allowed = StepStateMachine.CanTransition(from, to);
        Assert.Equal(expected, allowed);
    }
}
