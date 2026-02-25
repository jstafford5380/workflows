using System.Text.Json.Nodes;
using Engine.Core.Abstractions;
using Engine.Core.Domain;

namespace Engine.Activities;

public sealed class RoutedActivityRunner : IActivityRunner
{
    private readonly ScriptActivityRunner _scriptRunner;
    private readonly LocalActivityRunner _localRunner;

    public RoutedActivityRunner(ScriptActivityRunner scriptRunner, LocalActivityRunner localRunner)
    {
        _scriptRunner = scriptRunner;
        _localRunner = localRunner;
    }

    public Task<ActivityExecutionResult> RunAsync(ActivityExecutionRequest request, CancellationToken cancellationToken)
    {
        if (_scriptRunner.CanHandle(request.ActivityRef))
        {
            return _scriptRunner.RunAsync(request, cancellationToken);
        }

        if (request.ActivityRef.StartsWith("local.", StringComparison.OrdinalIgnoreCase))
        {
            return _localRunner.RunAsync(request, cancellationToken);
        }

        return Task.FromResult(new ActivityExecutionResult(
            false,
            new JsonObject(),
            $"Unknown activityRef '{request.ActivityRef}'. Configure it in Activities:ScriptMap or use local.* refs.",
            false));
    }
}
