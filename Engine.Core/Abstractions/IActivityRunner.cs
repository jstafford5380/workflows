using Engine.Core.Domain;

namespace Engine.Core.Abstractions;

public interface IActivityRunner
{
    Task<ActivityExecutionResult> RunAsync(ActivityExecutionRequest request, CancellationToken cancellationToken);
}
