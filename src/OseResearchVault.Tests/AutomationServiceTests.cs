using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AutomationServiceTests
{
    [Fact]
    public async Task CreateAndRunNow_PersistsAutomationAndRunHistory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var automationService = new SqliteAutomationService(settingsService);

            var automationId = await automationService.CreateAutomationAsync(new AutomationUpsertRequest
            {
                Name = "Daily refresh",
                Enabled = true,
                ScheduleType = "interval",
                IntervalMinutes = 30,
                PayloadType = "AskMyVault",
                QueryText = "What changed?"
            });

            var runId = await automationService.RunNowAsync(automationId);

            var all = await automationService.GetAutomationsAsync();
            var automation = Assert.Single(all);
            Assert.Equal("Daily refresh", automation.Name);
            Assert.Equal("success", automation.LastStatus);
            Assert.False(string.IsNullOrWhiteSpace(automation.NextRunAt));

            var runs = await automationService.GetRunsAsync(automationId);
            var run = Assert.Single(runs);
            Assert.Equal(runId, run.Id);
            Assert.Equal("manual", run.TriggerType);
            Assert.Equal("success", run.Status);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true, Pooling = false }.ToString());
            await connection.OpenAsync();

            var count = await connection.QuerySingleAsync<int>("SELECT COUNT(1) FROM automation_run WHERE automation_id = @Id", new { Id = automationId });
            Assert.Equal(1, count);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class TestAppSettingsService(string rootDirectory) : IAppSettingsService
    {
        private readonly AppSettings _settings = new()
        {
            DatabaseDirectory = rootDirectory,
            VaultStorageDirectory = rootDirectory
        };

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
