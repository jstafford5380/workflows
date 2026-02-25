using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Engine.Core.Abstractions;
using Engine.Core.Domain;
using Microsoft.Extensions.Logging;

namespace Engine.Activities;

public sealed class LocalActivityRunner : IActivityRunner
{
    private readonly ILogger<LocalActivityRunner> _logger;

    public LocalActivityRunner(ILogger<LocalActivityRunner> logger)
    {
        _logger = logger;
    }

    public Task<ActivityExecutionResult> RunAsync(ActivityExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Inputs["simulateFailure"]?.GetValue<bool>() == true)
        {
            return Task.FromResult(new ActivityExecutionResult(
                false,
                new JsonObject(),
                "Simulated failure requested by input.",
                true));
        }

        var outputs = request.Inputs.DeepClone()?.AsObject() ?? new JsonObject();
        outputs["activityRef"] = request.ActivityRef;
        outputs["instanceId"] = request.InstanceId.ToString();
        outputs["stepId"] = request.StepId;

        if (!outputs.ContainsKey("projectNumber"))
        {
            outputs["projectNumber"] = CreateStableProjectNumber(request.IdempotencyKey);
        }

        _logger.LogInformation(
            "Executed local activity {ActivityRef} for {InstanceId}/{StepId}",
            request.ActivityRef,
            request.InstanceId,
            request.StepId);

        return Task.FromResult(new ActivityExecutionResult(true, outputs, null));
    }

    private static string CreateStableProjectNumber(string idempotencyKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey));
        var value = Math.Abs(BitConverter.ToInt32(bytes, 0));
        return $"p-{value % 1_000_000_000:D9}";
    }
}
