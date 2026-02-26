using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class MirrorBackupSchedulerTests
{
    [Fact]
    public async Task StartAsync_WhenMirrorEnabled_ExportsBackupAndCreatesLogs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var mirrorFolder = Path.Combine(tempRoot, "mirror");
            var settingsService = new InMemoryAppSettingsService(new AppSettings
            {
                DatabaseDirectory = tempRoot,
                VaultStorageDirectory = tempRoot,
                CurrentWorkspaceId = "workspace-1",
                MirrorEnabled = true,
                MirrorFolderPath = mirrorFolder,
                MirrorFrequencyHours = 24
            });

            var backupService = new FakeBackupService();
            var notificationService = new FakeNotificationService();
            var shareLogService = new FakeShareLogService();
            var scheduler = new MirrorBackupScheduler(
                settingsService,
                backupService,
                notificationService,
                shareLogService,
                new FixedTimeProvider(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero)));

            await scheduler.StartAsync();
            await scheduler.StopAsync();

            Assert.Single(backupService.OutputPaths);
            Assert.True(File.Exists(backupService.OutputPaths[0]));
            Assert.Single(notificationService.Notifications);
            Assert.Single(shareLogService.Requests);
            Assert.Equal("workspace-1", shareLogService.Requests[0].WorkspaceId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class InMemoryAppSettingsService(AppSettings settings) : IAppSettingsService
    {
        private AppSettings _settings = settings;

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBackupService : IBackupService
    {
        public List<string> OutputPaths { get; } = [];

        public Task ExportWorkspaceBackupAsync(string workspaceId, string outputZipPath, CancellationToken cancellationToken = default)
        {
            OutputPaths.Add(outputZipPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath)!);
            File.WriteAllText(outputZipPath, "backup");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<(string Level, string Title, string Body)> Notifications { get; } = [];

        public Task AddNotification(string level, string title, string body, CancellationToken cancellationToken = default)
        {
            Notifications.Add((level, title, body));
            return Task.CompletedTask;
        }

        public Task MarkRead(string notificationId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NotificationRecord>> ListNotifications(string workspaceId, bool unreadOnly, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotificationRecord>>([]);
    }

    private sealed class FakeShareLogService : IShareLogService
    {
        public List<ShareLogCreateRequest> Requests { get; } = [];

        public Task AddAsync(ShareLogCreateRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ShareLogRecord>> GetRecentAsync(string workspaceId, int limit = 200, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ShareLogRecord>>([]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }
}
