using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class AutomationScheduler(
    IAutomationRepository automationRepository,
    IAutomationExecutor automationExecutor,
    TimeProvider? timeProvider = null) : IAutomationScheduler
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loopTask is not null)
        {
            return;
        }

        await RunOnceAsync(cancellationToken);

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_loopTask is null || _loopCts is null)
        {
            return;
        }

        _loopCts.Cancel();

        try
        {
            await _loopTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _loopCts.Dispose();
            _loopCts = null;
            _loopTask = null;
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            await EnsureNextRunTimesAsync(now, cancellationToken);

            var dueAutomations = await automationRepository.GetDueAutomationsAsync(now, cancellationToken);
            foreach (var automation in dueAutomations)
            {
                await ExecuteAutomationAsync(automation, now, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), _timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RunOnceAsync(cancellationToken);
        }
    }

    private async Task EnsureNextRunTimesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var enabled = await automationRepository.GetEnabledAutomationsAsync(cancellationToken);
        foreach (var automation in enabled.Where(a => string.IsNullOrWhiteSpace(a.NextRunAt)))
        {
            var nextRunAt = ComputeNextRunAt(automation, now);
            await automationRepository.UpdateScheduleAsync(automation.AutomationId, automation.LastRunAt, nextRunAt.ToString("O"), cancellationToken);
        }
    }

    private async Task ExecuteAutomationAsync(AutomationRecord automation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var startedAt = now.ToString("O");
        var automationRunId = await automationRepository.CreateAutomationRunAsync(automation.AutomationId, startedAt, cancellationToken);

        var result = await automationExecutor.ExecuteAsync(automation, cancellationToken);
        var endedAt = _timeProvider.GetUtcNow();
        var nextRunAt = ComputeNextRunAt(automation, endedAt);

        await automationRepository.CompleteAutomationRunAsync(
            automationRunId,
            result.Success ? "success" : "fail",
            endedAt.ToString("O"),
            result.Error,
            result.CreatedRunId,
            cancellationToken);

        await automationRepository.UpdateScheduleAsync(
            automation.AutomationId,
            startedAt,
            nextRunAt.ToString("O"),
            cancellationToken);
    }

    private static DateTimeOffset ComputeNextRunAt(AutomationRecord automation, DateTimeOffset fromUtc)
    {
        if (string.Equals(automation.ScheduleType, "interval", StringComparison.OrdinalIgnoreCase))
        {
            var minutes = automation.IntervalMinutes.GetValueOrDefault(60);
            return fromUtc.AddMinutes(minutes);
        }

        if (string.Equals(automation.ScheduleType, "daily", StringComparison.OrdinalIgnoreCase))
        {
            if (!TimeSpan.TryParse(automation.DailyTime, out var dailyTime))
            {
                dailyTime = TimeSpan.FromHours(9);
            }

            var localNow = fromUtc.ToLocalTime();
            var localCandidate = new DateTimeOffset(
                localNow.Year,
                localNow.Month,
                localNow.Day,
                dailyTime.Hours,
                dailyTime.Minutes,
                0,
                localNow.Offset);

            if (localCandidate <= localNow)
            {
                localCandidate = localCandidate.AddDays(1);
            }

            return localCandidate.ToUniversalTime();
        }

        return fromUtc.AddMinutes(60);
    }
}
