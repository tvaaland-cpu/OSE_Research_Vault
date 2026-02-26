using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Repositories;

public sealed class SqliteAutomationRepository(IAppSettingsService appSettingsService) : IAutomationRepository
{
    public async Task<string> CreateAutomationAsync(AutomationRecord automation, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO automation (automation_id, workspace_id, name, is_enabled, schedule_type, interval_minutes, daily_time, last_run_at, next_run_at, payload_json, created_at, updated_at)
              VALUES (@AutomationId, @WorkspaceId, @Name, @IsEnabled, @ScheduleType, @IntervalMinutes, @DailyTime, @LastRunAt, @NextRunAt, @PayloadJson, @CreatedAt, @UpdatedAt)",
            new
            {
                automation.AutomationId,
                automation.WorkspaceId,
                automation.Name,
                IsEnabled = automation.IsEnabled ? 1 : 0,
                automation.ScheduleType,
                automation.IntervalMinutes,
                automation.DailyTime,
                automation.LastRunAt,
                automation.NextRunAt,
                automation.PayloadJson,
                automation.CreatedAt,
                automation.UpdatedAt
            }, cancellationToken: cancellationToken));

        return automation.AutomationId;
    }

    public async Task<IReadOnlyList<AutomationRecord>> GetEnabledAutomationsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AutomationRecord>(new CommandDefinition(
            @"SELECT automation_id AS AutomationId,
                     workspace_id AS WorkspaceId,
                     name,
                     is_enabled AS IsEnabled,
                     schedule_type AS ScheduleType,
                     interval_minutes AS IntervalMinutes,
                     daily_time AS DailyTime,
                     last_run_at AS LastRunAt,
                     next_run_at AS NextRunAt,
                     payload_json AS PayloadJson,
                     created_at AS CreatedAt,
                     updated_at AS UpdatedAt
                FROM automation
               WHERE is_enabled = 1", cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<AutomationRecord>> GetDueAutomationsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AutomationRecord>(new CommandDefinition(
            @"SELECT automation_id AS AutomationId,
                     workspace_id AS WorkspaceId,
                     name,
                     is_enabled AS IsEnabled,
                     schedule_type AS ScheduleType,
                     interval_minutes AS IntervalMinutes,
                     daily_time AS DailyTime,
                     last_run_at AS LastRunAt,
                     next_run_at AS NextRunAt,
                     payload_json AS PayloadJson,
                     created_at AS CreatedAt,
                     updated_at AS UpdatedAt
                FROM automation
               WHERE is_enabled = 1
                 AND next_run_at IS NOT NULL
                 AND next_run_at <= @Now
            ORDER BY next_run_at", new { Now = now.ToString("O") }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task UpdateScheduleAsync(string automationId, string? lastRunAt, string nextRunAt, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE automation
                 SET last_run_at = @LastRunAt,
                     next_run_at = @NextRunAt,
                     updated_at = @UpdatedAt
               WHERE automation_id = @AutomationId",
            new
            {
                AutomationId = automationId,
                LastRunAt = lastRunAt,
                NextRunAt = nextRunAt,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            }, cancellationToken: cancellationToken));
    }

    public async Task<string> CreateAutomationRunAsync(string automationId, string startedAt, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var automationRunId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO automation_run (automation_run_id, automation_id, started_at, ended_at, status, error, created_run_id)
              VALUES (@AutomationRunId, @AutomationId, @StartedAt, NULL, 'running', NULL, NULL)",
            new { AutomationRunId = automationRunId, AutomationId = automationId, StartedAt = startedAt }, cancellationToken: cancellationToken));

        return automationRunId;
    }

    public async Task CompleteAutomationRunAsync(string automationRunId, string status, string endedAt, string? error, string? createdRunId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE automation_run
                 SET status = @Status,
                     ended_at = @EndedAt,
                     error = @Error,
                     created_run_id = @CreatedRunId
               WHERE automation_run_id = @AutomationRunId",
            new { AutomationRunId = automationRunId, Status = status, EndedAt = endedAt, Error = error, CreatedRunId = createdRunId }, cancellationToken: cancellationToken));
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
}
