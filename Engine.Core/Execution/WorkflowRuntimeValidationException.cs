namespace Engine.Core.Execution;

public sealed class WorkflowRuntimeValidationException : Exception
{
    public WorkflowRuntimeValidationException(string message)
        : base(message)
    {
    }
}
