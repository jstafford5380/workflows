using Engine.Core.Definitions;

namespace Engine.Core.Execution;

public static class BackoffCalculator
{
    public static TimeSpan CalculateDelay(RetryPolicyDefinition policy, int failedAttempt)
    {
        var exponent = Math.Max(0, failedAttempt - 1);
        var delaySeconds = policy.InitialDelaySeconds * Math.Pow(policy.BackoffFactor, exponent);
        var boundedSeconds = Math.Min(policy.MaxDelaySeconds, delaySeconds);
        return TimeSpan.FromSeconds(Math.Max(1, boundedSeconds));
    }
}
