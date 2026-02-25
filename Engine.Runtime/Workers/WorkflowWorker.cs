using Engine.Core.Abstractions;
using Engine.Core.Domain;
using Engine.Core.Execution;
using Engine.Runtime.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Engine.Runtime.Workers;

public sealed class WorkflowWorker : BackgroundService
{
    private static readonly TimeSpan QueueLeaseDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StepLeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LeaseRenewInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<WorkflowWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public WorkflowWorker(IServiceScopeFactory scopeFactory, IClock clock, ILogger<WorkflowWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outboxDispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                var workQueue = scope.ServiceProvider.GetRequiredService<IWorkQueue>();

                await outboxDispatcher.DispatchBatchAsync(50, stoppingToken);

                var queueLeaseExpiresAt = _clock.UtcNow.Add(QueueLeaseDuration);
                var workItem = await workQueue.TryDequeueAsync(_workerId, queueLeaseExpiresAt, _clock.UtcNow, stoppingToken);
                if (workItem is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    continue;
                }

                await ProcessWorkItemAsync(scope.ServiceProvider, workQueue, workItem, stoppingToken);
                await workQueue.MarkCompletedAsync(workItem.WorkItemId, _clock.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow worker loop failed");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task ProcessWorkItemAsync(
        IServiceProvider services,
        IWorkQueue workQueue,
        WorkQueueItemRecord workItem,
        CancellationToken cancellationToken)
    {
        if (workItem.Kind != WorkItemKind.ExecuteStep)
        {
            _logger.LogWarning("Skipping unsupported work item kind {Kind}", workItem.Kind);
            return;
        }

        var payload = WorkItemPayload.FromJson(workItem.Payload);
        var instanceRepository = services.GetRequiredService<IInstanceRepository>();
        var activityRunner = services.GetRequiredService<IActivityRunner>();

        var stepLeaseExpiresAt = _clock.UtcNow.Add(StepLeaseDuration);
        var claimedStep = await instanceRepository.TryClaimRunnableStepAsync(
            payload.InstanceId,
            payload.StepId,
            _workerId,
            stepLeaseExpiresAt,
            _clock.UtcNow,
            cancellationToken);

        if (claimedStep is null)
        {
            return;
        }

        var workflowInstance = await instanceRepository.GetInstanceAsync(payload.InstanceId, cancellationToken);
        if (workflowInstance is null || workflowInstance.Status != WorkflowInstanceStatus.Running)
        {
            return;
        }

        if (claimedStep.StepDefinition.WaitForEvent is not null)
        {
            var correlationKey = BindingResolver.ResolveCorrelationKey(
                claimedStep.StepDefinition.WaitForEvent.CorrelationKeyExpression,
                payload.InstanceId,
                workflowInstance.Inputs);

            var subscription = new EventSubscriptionRecord(
                Guid.NewGuid(),
                payload.InstanceId,
                claimedStep.StepId,
                claimedStep.StepDefinition.WaitForEvent.EventType,
                correlationKey,
                EventSubscriptionStatus.Waiting,
                _clock.UtcNow,
                null,
                null);

            await instanceRepository.MarkStepWaitingForEventAsync(
                payload.InstanceId,
                claimedStep.StepId,
                _workerId,
                subscription,
                _clock.UtcNow,
                cancellationToken);
            return;
        }

        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (claimedStep.StepDefinition.TimeoutSeconds is { } timeoutSeconds)
        {
            executionCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        }

        var leaseRenewalTask = RenewLeasesWhileRunningAsync(
            workQueue,
            instanceRepository,
            workItem.WorkItemId,
            payload.InstanceId,
            claimedStep.StepId,
            executionCts.Token);

        try
        {
            var stepOutputs = await instanceRepository.GetStepOutputsAsync(payload.InstanceId, executionCts.Token);
            var resolvedInputs = BindingResolver.ResolveStepInputs(
                claimedStep.StepDefinition,
                payload.InstanceId,
                workflowInstance.Inputs,
                stepOutputs);

            var executionRequest = new ActivityExecutionRequest(
                payload.InstanceId,
                claimedStep.StepId,
                claimedStep.ActivityRef,
                resolvedInputs,
                claimedStep.IdempotencyKey,
                claimedStep.StepDefinition.ScriptParameters);

            var activityResult = await activityRunner.RunAsync(executionRequest, executionCts.Token);
            var capturedConsoleOutput = activityResult.ConsoleOutput;
            if (string.IsNullOrWhiteSpace(capturedConsoleOutput) && !activityResult.IsSuccess)
            {
                capturedConsoleOutput = $"No script console output captured.{Environment.NewLine}Error: {activityResult.ErrorMessage ?? "Activity failed."}";
            }

            if (!string.IsNullOrWhiteSpace(capturedConsoleOutput))
            {
                await instanceRepository.SaveStepExecutionLogAsync(
                    payload.InstanceId,
                    claimedStep.StepId,
                    claimedStep.Attempt,
                    activityResult.IsSuccess,
                    capturedConsoleOutput,
                    _clock.UtcNow,
                    executionCts.Token);
            }

            if (activityResult.IsSuccess)
            {
                await instanceRepository.MarkStepSucceededAsync(
                    payload.InstanceId,
                    claimedStep.StepId,
                    _workerId,
                    activityResult.Outputs,
                    _clock.UtcNow,
                    [],
                    executionCts.Token);

                await instanceRepository.PromoteDependentsToRunnableAsync(
                    payload.InstanceId,
                    claimedStep.StepId,
                    _clock.UtcNow,
                    executionCts.Token);

                await instanceRepository.MarkInstanceSucceededIfCompleteAsync(
                    payload.InstanceId,
                    _clock.UtcNow,
                    executionCts.Token);

                return;
            }

            await HandleFailureAsync(instanceRepository, payload, claimedStep, activityResult.ErrorMessage ?? "Activity failed", activityResult.IsRetryable, executionCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await instanceRepository.SaveStepExecutionLogAsync(
                payload.InstanceId,
                claimedStep.StepId,
                claimedStep.Attempt,
                false,
                "Step execution timed out before completion.",
                _clock.UtcNow,
                CancellationToken.None);

            await HandleFailureAsync(instanceRepository, payload, claimedStep, "Step timed out.", true, CancellationToken.None);
        }
        catch (WorkflowRuntimeValidationException ex)
        {
            _logger.LogWarning(
                "Runtime validation failed for {InstanceId}/{StepId}: {Message}",
                payload.InstanceId,
                payload.StepId,
                ex.Message);

            await instanceRepository.SaveStepExecutionLogAsync(
                payload.InstanceId,
                claimedStep.StepId,
                claimedStep.Attempt,
                false,
                $"Input validation failed before activity execution.{Environment.NewLine}Details: {ex.Message}",
                _clock.UtcNow,
                CancellationToken.None);

            await HandleFailureAsync(instanceRepository, payload, claimedStep, ex.Message, false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step execution failed for {InstanceId}/{StepId}", payload.InstanceId, payload.StepId);

            await instanceRepository.SaveStepExecutionLogAsync(
                payload.InstanceId,
                claimedStep.StepId,
                claimedStep.Attempt,
                false,
                $"Engine execution failed before activity completion.{Environment.NewLine}{ex}",
                _clock.UtcNow,
                CancellationToken.None);

            await HandleFailureAsync(instanceRepository, payload, claimedStep, ex.Message, true, CancellationToken.None);
        }
        finally
        {
            executionCts.Cancel();
            try
            {
                await leaseRenewalTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task HandleFailureAsync(
        IInstanceRepository instanceRepository,
        WorkItemPayload payload,
        StepRunRecord claimedStep,
        string error,
        bool retryable,
        CancellationToken cancellationToken)
    {
        var policy = claimedStep.StepDefinition.RetryPolicy;
        var failedAttempt = claimedStep.Attempt;
        var hasAttemptsRemaining = failedAttempt < policy.MaxAttempts;
        var abortWorkflow = claimedStep.StepDefinition.AbortOnFail;
        var shouldRetry = !abortWorkflow && retryable && hasAttemptsRemaining;

        var outboxMessages = new List<OutboxMessageRecord>();
        DateTimeOffset? nextAttemptAt = null;
        if (shouldRetry)
        {
            var delay = BackoffCalculator.CalculateDelay(policy, failedAttempt);
            nextAttemptAt = _clock.UtcNow.Add(delay);
            outboxMessages.Add(WorkflowEngineService.CreateEnqueueOutbox(payload.InstanceId, payload.StepId, nextAttemptAt.Value));
        }

        await instanceRepository.MarkStepFailureAsync(
            payload.InstanceId,
            payload.StepId,
            _workerId,
            error,
            shouldRetry,
            abortWorkflow,
            nextAttemptAt,
            _clock.UtcNow,
            outboxMessages,
            cancellationToken);
    }

    private async Task RenewLeasesWhileRunningAsync(
        IWorkQueue workQueue,
        IInstanceRepository instanceRepository,
        Guid workItemId,
        Guid instanceId,
        string stepId,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(LeaseRenewInterval, cancellationToken);

            var now = _clock.UtcNow;
            await workQueue.RenewLeaseAsync(workItemId, _workerId, now.Add(QueueLeaseDuration), cancellationToken);
            await instanceRepository.RenewStepLeaseAsync(instanceId, stepId, _workerId, now.Add(StepLeaseDuration), cancellationToken);
        }
    }
}
