namespace Engine.Core.Domain;

public enum WorkflowInstanceStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    Canceled = 3,
    Paused = 4
}

public enum StepRunStatus
{
    Pending = 0,
    Runnable = 1,
    Running = 2,
    Waiting = 3,
    Succeeded = 4,
    Failed = 5,
    Canceled = 6,
    Aborted = 7
}

public enum EventSubscriptionStatus
{
    Waiting = 0,
    Fulfilled = 1,
    Canceled = 2
}

public enum OutboxMessageType
{
    EnqueueWorkItem = 0,
    PublishNotification = 1
}

public enum WorkItemKind
{
    ExecuteStep = 0
}
