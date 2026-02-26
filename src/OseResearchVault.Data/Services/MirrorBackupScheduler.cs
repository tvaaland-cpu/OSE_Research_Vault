using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class MirrorBackupScheduler(
    IAppSettingsService appSettingsService,
    IBackupService backupService,
    INotificationService notificationService,
    IShareLogService shareLogService,
    TimeProvider? timeProvider = null) : IMirrorBackupScheduler
{
    private const int RetainedBackupCount = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
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

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval, _timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RunOnceAsync(cancellationToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
            if (!settings.MirrorEnabled || string.IsNullOrWhiteSpace(settings.MirrorFolderPath))
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var frequency = TimeSpan.FromHours(Math.Max(1, settings.MirrorFrequencyHours));
            if (TryGetLastRun(settings, out var lastRunAt) && now - lastRunAt < frequency)
            {
                return;
            }

            Directory.CreateDirectory(settings.MirrorFolderPath);
            var backupPath = Path.Combine(settings.MirrorFolderPath, $"workspace-backup-{now:yyyyMMdd-HHmmss}.zip");
            await backupService.ExportWorkspaceBackupAsync(string.Empty, backupPath, cancellationToken);

            settings.MirrorLastRunAt = now.ToString("O");
            await appSettingsService.SaveSettingsAsync(settings, cancellationToken);

            await notificationService.AddNotification("info", "Mirror backup created", $"Backup saved to: {backupPath}", cancellationToken);
            await AddShareLogEntryAsync(settings, backupPath, cancellationToken);
            DeleteOldBackups(settings.MirrorFolderPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool TryGetLastRun(AppSettings settings, out DateTimeOffset lastRunAt)
    {
        if (DateTimeOffset.TryParse(settings.MirrorLastRunAt, out lastRunAt))
        {
            return true;
        }

        lastRunAt = default;
        return false;
    }

    private async Task AddShareLogEntryAsync(AppSettings settings, string backupPath, CancellationToken cancellationToken)
    {
        try
        {
            await shareLogService.AddAsync(new ShareLogCreateRequest
            {
                WorkspaceId = settings.CurrentWorkspaceId ?? string.Empty,
                Action = "workspace_backup_mirror",
                OutputPath = backupPath,
                Summary = "Workspace mirror backup export"
            }, cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // share_log table may not exist on older databases.
        }
    }

    private static void DeleteOldBackups(string mirrorFolderPath)
    {
        var backups = new DirectoryInfo(mirrorFolderPath)
            .EnumerateFiles("workspace-backup-*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.CreationTimeUtc)
            .ToList();

        foreach (var file in backups.Skip(RetainedBackupCount))
        {
            file.Delete();
        }
    }
}
