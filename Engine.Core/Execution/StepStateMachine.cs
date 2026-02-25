using Engine.Core.Domain;

namespace Engine.Core.Execution;

public static class StepStateMachine
{
    private static readonly Dictionary<StepRunStatus, HashSet<StepRunStatus>> AllowedTransitions = new()
    {
        [StepRunStatus.Pending] = [StepRunStatus.Runnable, StepRunStatus.Canceled, StepRunStatus.Aborted],
        [StepRunStatus.Runnable] = [StepRunStatus.Running, StepRunStatus.Canceled, StepRunStatus.Aborted],
        [StepRunStatus.Running] = [StepRunStatus.Succeeded, StepRunStatus.Failed, StepRunStatus.Runnable, StepRunStatus.Waiting, StepRunStatus.Canceled, StepRunStatus.Aborted],
        [StepRunStatus.Waiting] = [StepRunStatus.Succeeded, StepRunStatus.Failed, StepRunStatus.Canceled, StepRunStatus.Aborted],
        [StepRunStatus.Failed] = [StepRunStatus.Runnable, StepRunStatus.Canceled],
        [StepRunStatus.Succeeded] = [],
        [StepRunStatus.Canceled] = [],
        [StepRunStatus.Aborted] = []
    };

    public static bool CanTransition(StepRunStatus from, StepRunStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
