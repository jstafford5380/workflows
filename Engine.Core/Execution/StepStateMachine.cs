using Engine.Core.Domain;

namespace Engine.Core.Execution;

public static class StepStateMachine
{
    private static readonly Dictionary<StepRunStatus, HashSet<StepRunStatus>> AllowedTransitions = new()
    {
        [StepRunStatus.Pending] = [StepRunStatus.Runnable, StepRunStatus.Canceled],
        [StepRunStatus.Runnable] = [StepRunStatus.Running, StepRunStatus.Canceled],
        [StepRunStatus.Running] = [StepRunStatus.Succeeded, StepRunStatus.Failed, StepRunStatus.Runnable, StepRunStatus.Waiting, StepRunStatus.Canceled],
        [StepRunStatus.Waiting] = [StepRunStatus.Succeeded, StepRunStatus.Failed, StepRunStatus.Canceled],
        [StepRunStatus.Failed] = [StepRunStatus.Runnable, StepRunStatus.Canceled],
        [StepRunStatus.Succeeded] = [],
        [StepRunStatus.Canceled] = []
    };

    public static bool CanTransition(StepRunStatus from, StepRunStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
