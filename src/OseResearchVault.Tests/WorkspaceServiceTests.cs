using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task CreateAndSwitch_PersistsCurrentWorkspaceId()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-workspace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var settings = new AppSettings
            {
                DatabaseDirectory = Path.Combine(tempRoot, "default", "data"),
                VaultStorageDirectory = Path.Combine(tempRoot, "default", "vault"),
                ImportInboxFolderPath = Path.Combine(tempRoot, "inbox")
            };

            var settingsService = new InMemoryAppSettingsService(settings);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            var service = new WorkspaceService(settingsService, initializer, new NoOpAutomationScheduler());

            var one = await service.CreateAsync("Workspace One", Path.Combine(tempRoot, "ws1"));
            var two = await service.CreateAsync("Workspace Two", Path.Combine(tempRoot, "ws2"));

            Assert.Equal(two.Id, (await settingsService.GetSettingsAsync()).CurrentWorkspaceId);

            var switched = await service.SwitchAsync(one.Id);

            Assert.True(switched);
            var persisted = await settingsService.GetSettingsAsync();
            Assert.Equal(one.Id, persisted.CurrentWorkspaceId);
            Assert.EndsWith(Path.Combine("ws1", "data"), persisted.DatabaseDirectory);
        }
        finally
        {
            TestCleanup.DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task CloneWorkspace_CopiesDatabaseAndVaultAndSetsCurrentWorkspace()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-workspace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settings = new AppSettings
            {
                DatabaseDirectory = Path.Combine(tempRoot, "default", "data"),
                VaultStorageDirectory = Path.Combine(tempRoot, "default", "vault"),
                ImportInboxFolderPath = Path.Combine(tempRoot, "inbox")
            };

            var settingsService = new InMemoryAppSettingsService(settings);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            var service = new WorkspaceService(settingsService, initializer, new NoOpAutomationScheduler());

            var source = await service.CreateAsync("Source", Path.Combine(tempRoot, "source"));

            var sourceSettings = await settingsService.GetSettingsAsync();
            var sourceVaultFile = Path.Combine(sourceSettings.VaultStorageDirectory, "evidence", "note.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceVaultFile)!);
            await File.WriteAllTextAsync(sourceVaultFile, "copied");

            await using (var sourceConnection = new SqliteConnection($"Data Source={sourceSettings.DatabaseFilePath}"))
            {
                await sourceConnection.OpenAsync();
                var workspaceId = await sourceConnection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
                Assert.False(string.IsNullOrWhiteSpace(workspaceId));
            }

            var destinationPath = Path.Combine(tempRoot, "cloned-workspace");
            var cloned = await service.CloneWorkspaceAsync(source.Id, destinationPath, "Source Clone");

            Assert.Equal(cloned.Id, (await settingsService.GetSettingsAsync()).CurrentWorkspaceId);

            var clonedDatabasePath = Path.Combine(destinationPath, "data", "ose-research-vault.db");
            Assert.True(File.Exists(clonedDatabasePath));

            await using (var clonedConnection = new SqliteConnection($"Data Source={clonedDatabasePath}"))
            {
                await clonedConnection.OpenAsync();
                var workspaceRows = await clonedConnection.QuerySingleAsync<long>("SELECT COUNT(*) FROM workspace");
                Assert.True(workspaceRows > 0);
            }

            var clonedVaultFile = Path.Combine(destinationPath, "vault", "evidence", "note.txt");
            Assert.True(File.Exists(clonedVaultFile));
            Assert.Equal("copied", await File.ReadAllTextAsync(clonedVaultFile));
        }
        finally
        {
            TestCleanup.DeleteDirectory(tempRoot);
        }
    }

    private sealed class InMemoryAppSettingsService(AppSettings settings) : IAppSettingsService
    {
        private AppSettings _settings = settings;

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpAutomationScheduler : IAutomationScheduler
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RunOnceAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
