using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AutomationSchedulerTests
{
    [Fact]
    public async Task RunOnce_ComputesMissingNextRunAt_Deterministically()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var automationRepository = new SqliteAutomationRepository(settingsService);
            var now = new DateTimeOffset(2024, 01, 01, 10, 00, 00, TimeSpan.Zero);
            var timeProvider = new FixedTimeProvider(now);
            var scheduler = new AutomationScheduler(automationRepository, new NoOpAutomationExecutor(), timeProvider);

            var workspaceId = await EnsureWorkspaceAsync(settingsService);
            await automationRepository.CreateAutomationAsync(new AutomationRecord
            {
                AutomationId = Guid.NewGuid().ToString(),
                WorkspaceId = workspaceId,
                Name = "Hourly check",
                IsEnabled = true,
                ScheduleType = "interval",
                IntervalMinutes = 15,
                DailyTime = null,
                LastRunAt = null,
                NextRunAt = null,
                PayloadJson = "{\"type\":\"agent_run_stub\",\"agent_id\":\"x\"}",
                CreatedAt = now.ToString("O"),
                UpdatedAt = now.ToString("O")
            });

            await scheduler.RunOnceAsync();

            var nextRunAt = await QuerySingleAsync<string>(settingsService, "SELECT next_run_at FROM automation");
            Assert.Equal(now.AddMinutes(15).ToString("O"), nextRunAt);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RunOnce_DisabledAutomationsNeverRun()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var automationRepository = new SqliteAutomationRepository(settingsService);
            var now = new DateTimeOffset(2024, 01, 01, 10, 00, 00, TimeSpan.Zero);
            var timeProvider = new FixedTimeProvider(now);
            var scheduler = new AutomationScheduler(automationRepository, new NoOpAutomationExecutor(), timeProvider);

            var workspaceId = await EnsureWorkspaceAsync(settingsService);
            await automationRepository.CreateAutomationAsync(new AutomationRecord
            {
                AutomationId = Guid.NewGuid().ToString(),
                WorkspaceId = workspaceId,
                Name = "Disabled",
                IsEnabled = false,
                ScheduleType = "interval",
                IntervalMinutes = 1,
                DailyTime = null,
                LastRunAt = null,
                NextRunAt = now.AddMinutes(-1).ToString("O"),
                PayloadJson = JsonSerializer.Serialize(new { type = "agent_run_stub", agent_id = "missing" }),
                CreatedAt = now.ToString("O"),
                UpdatedAt = now.ToString("O")
            });

            await scheduler.RunOnceAsync();

            var runCount = await QuerySingleAsync<int>(settingsService, "SELECT COUNT(1) FROM automation_run");
            Assert.Equal(0, runCount);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task<string> EnsureWorkspaceAsync(IAppSettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync();

        var workspaceId = await connection.ExecuteScalarAsync<string?>("SELECT id FROM workspace LIMIT 1");
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaceId;
        }

        workspaceId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync(
            "INSERT INTO workspace (id, name, base_path, created_at, updated_at) VALUES (@Id, 'Default Workspace', '', @Now, @Now)",
            new { Id = workspaceId, Now = DateTimeOffset.UtcNow.ToString("O") });

        return workspaceId;
    }

    private static async Task<T> QuerySingleAsync<T>(IAppSettingsService settingsService, string sql)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync();
        return await connection.QuerySingleAsync<T>(sql);
    }

    private static SqliteConnection OpenConnection(string databaseFilePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            ForeignKeys = true
        }.ToString();

        return new SqliteConnection(connectionString);
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

    private sealed class NoOpAutomationExecutor : IAutomationExecutor
    {
        public Task<AutomationExecutionResult> ExecuteAsync(AutomationRecord automation, CancellationToken cancellationToken = default)
            => Task.FromResult(new AutomationExecutionResult { Success = true });
    }

    private sealed class FixedTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }
}
