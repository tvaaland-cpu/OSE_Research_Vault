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

        var settings = new AppSettings
        {
            DatabaseDirectory = Path.Combine(tempRoot, "default", "data"),
            VaultStorageDirectory = Path.Combine(tempRoot, "default", "vault"),
            ImportInboxFolderPath = Path.Combine(tempRoot, "inbox")
        };

        var settingsService = new InMemoryAppSettingsService(settings);
        var service = new WorkspaceService(settingsService, new NoOpDatabaseInitializer(), new NoOpAutomationScheduler());

        var one = await service.CreateAsync("Workspace One", Path.Combine(tempRoot, "ws1"));
        var two = await service.CreateAsync("Workspace Two", Path.Combine(tempRoot, "ws2"));

        Assert.Equal(two.Id, (await settingsService.GetSettingsAsync()).CurrentWorkspaceId);

        var switched = await service.SwitchAsync(one.Id);

        Assert.True(switched);
        var persisted = await settingsService.GetSettingsAsync();
        Assert.Equal(one.Id, persisted.CurrentWorkspaceId);
        Assert.EndsWith(Path.Combine("ws1", "data"), persisted.DatabaseDirectory);
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

    private sealed class NoOpDatabaseInitializer : IDatabaseInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpAutomationScheduler : IAutomationScheduler
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
