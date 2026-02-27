using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteAutomationService(IAppSettingsService appSettingsService) : IAutomationService
{
    public async Task<IReadOnlyList<AutomationRecord>> GetAutomationsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AutomationRecord>(new CommandDefinition(
            @"SELECT a.automation_id AS Id,
                     a.name,
                     a.is_enabled AS Enabled,
                     a.schedule_type AS ScheduleType,
                     a.interval_minutes AS IntervalMinutes,
                     a.daily_time AS DailyTime,
                     a.payload_json AS PayloadJson,
                     a.next_run_at AS NextRunAt,
                     a.last_run_at AS LastRunAt,
                     COALESCE(
                         (SELECT ar.status
                            FROM automation_run ar
                           WHERE ar.automation_id = a.automation_id
                        ORDER BY ar.started_at DESC
                           LIMIT 1),
                         ''
                     ) AS LastStatus
                FROM automation a
            ORDER BY a.created_at DESC", cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<string> CreateAutomationAsync(AutomationUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow;
        var automationId = Guid.NewGuid().ToString();
        var nextRun = ComputeNextRunAt(request, now);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO automation (automation_id, workspace_id, name, is_enabled, schedule_type, interval_minutes, daily_time, last_run_at, next_run_at, payload_json, created_at, updated_at)
              VALUES (@AutomationId, @WorkspaceId, @Name, @Enabled, @ScheduleType, @IntervalMinutes, @DailyTime, NULL, @NextRunAt, @PayloadJson, @Now, @Now)",
            new
            {
                AutomationId = automationId,
                WorkspaceId = workspaceId,
                Name = request.Name.Trim(),
                Enabled = request.Enabled ? 1 : 0,
                ScheduleType = request.ScheduleType,
                request.IntervalMinutes,
                DailyTime = Clean(request.DailyTime),
                NextRunAt = request.Enabled ? nextRun : null,
                PayloadJson = BuildPayloadJson(request),
                Now = now.ToString("O")
            }, cancellationToken: cancellationToken));

        return automationId;
    }

    public async Task UpdateAutomationAsync(string automationId, AutomationUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var nextRun = ComputeNextRunAt(request, now);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE automation
                 SET name = @Name,
                     is_enabled = @Enabled,
                     schedule_type = @ScheduleType,
                     interval_minutes = @IntervalMinutes,
                     daily_time = @DailyTime,
                     next_run_at = @NextRunAt,
                     payload_json = @PayloadJson,
                     updated_at = @Now
               WHERE automation_id = @AutomationId",
            new
            {
                AutomationId = automationId,
                Name = request.Name.Trim(),
                Enabled = request.Enabled ? 1 : 0,
                ScheduleType = request.ScheduleType,
                request.IntervalMinutes,
                DailyTime = Clean(request.DailyTime),
                NextRunAt = request.Enabled ? nextRun : null,
                PayloadJson = BuildPayloadJson(request),
                Now = now.ToString("O")
            }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAutomationAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM automation WHERE automation_id = @AutomationId", new { AutomationId = automationId }, cancellationToken: cancellationToken));
    }

    public async Task SetAutomationEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var schedule = await connection.QuerySingleOrDefaultAsync<(string ScheduleType, int? IntervalMinutes, string? DailyTime)>(new CommandDefinition(
            "SELECT schedule_type as ScheduleType, interval_minutes as IntervalMinutes, daily_time as DailyTime FROM automation WHERE automation_id = @AutomationId",
            new { AutomationId = automationId }, cancellationToken: cancellationToken));

        var now = DateTime.UtcNow;
        var nextRunAt = enabled
            ? ComputeNextRunAt(new AutomationUpsertRequest { ScheduleType = schedule.ScheduleType, IntervalMinutes = schedule.IntervalMinutes, DailyTime = schedule.DailyTime }, now)
            : null;

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE automation
                 SET is_enabled = @Enabled,
                     next_run_at = @NextRunAt,
                     updated_at = @Now
               WHERE automation_id = @AutomationId",
            new { AutomationId = automationId, Enabled = enabled ? 1 : 0, NextRunAt = nextRunAt, Now = now.ToString("O") }, cancellationToken: cancellationToken));
    }

    public async Task<string> RunNowAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var runId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO automation_run (automation_run_id, automation_id, started_at, ended_at, status, error, created_run_id)
              VALUES (@RunId, @AutomationId, @Now, @Now, 'success', NULL, NULL)",
            new { RunId = runId, AutomationId = automationId, Now = now.ToString("O") }, cancellationToken: cancellationToken));

        var schedule = await connection.QuerySingleOrDefaultAsync<(string ScheduleType, int? IntervalMinutes, string? DailyTime, int IsEnabled)>(new CommandDefinition(
            "SELECT schedule_type as ScheduleType, interval_minutes as IntervalMinutes, daily_time as DailyTime, is_enabled as IsEnabled FROM automation WHERE automation_id = @AutomationId",
            new { AutomationId = automationId }, cancellationToken: cancellationToken));

        var nextRunAt = schedule.IsEnabled == 1
            ? ComputeNextRunAt(new AutomationUpsertRequest { ScheduleType = schedule.ScheduleType, IntervalMinutes = schedule.IntervalMinutes, DailyTime = schedule.DailyTime }, now)
            : null;

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE automation
                 SET last_run_at = @Now,
                     next_run_at = @NextRunAt,
                     updated_at = @Now
               WHERE automation_id = @AutomationId",
            new { AutomationId = automationId, Now = now.ToString("O"), NextRunAt = nextRunAt }, cancellationToken: cancellationToken));

        return runId;
    }

    public async Task<IReadOnlyList<AutomationRunRecord>> GetRunsAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AutomationRunRecord>(new CommandDefinition(
            @"SELECT automation_run_id AS Id,
                     automation_id AS AutomationId,
                     'manual' AS TriggerType,
                     status,
                     started_at AS StartedAt,
                     ended_at AS FinishedAt
                FROM automation_run
               WHERE automation_id = @AutomationId
            ORDER BY started_at DESC",
            new { AutomationId = automationId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static string BuildPayloadJson(AutomationUpsertRequest request)
    {
        return JsonSerializer.Serialize(new
        {
            type = request.PayloadType,
            agentId = Clean(request.AgentId),
            query = request.QueryText?.Trim(),
            companyScopeMode = request.CompanyScopeMode,
            companyScopeIds = request.CompanyScopeIds
        });
    }

    private static string? ComputeNextRunAt(AutomationUpsertRequest request, DateTime nowUtc)
    {
        if (string.Equals(request.ScheduleType, "daily", StringComparison.OrdinalIgnoreCase))
        {
            if (!TimeSpan.TryParse(request.DailyTime, out var dailyTime))
            {
                dailyTime = TimeSpan.FromHours(9);
            }

            var today = nowUtc.Date.Add(dailyTime);
            var next = today <= nowUtc ? today.AddDays(1) : today;
            return next.ToString("O");
        }

        var minutes = Math.Max(1, request.IntervalMinutes ?? 60);
        return nowUtc.AddMinutes(minutes).ToString("O");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Pooling = false
        };

        return new SqliteConnection(builder.ToString());
    }

    private static async Task<string> EnsureWorkspaceAsync(string databaseFilePath, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(databaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var existing = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT id FROM workspace ORDER BY created_at LIMIT 1", cancellationToken: cancellationToken));
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var workspaceId = "default";
        var now = DateTime.UtcNow.ToString("O");
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO workspace (id, name, description, created_at, updated_at) VALUES (@Id, @Name, @Description, @Now, @Now)",
            new { Id = workspaceId, Name = "Default Workspace", Description = "Created automatically", Now = now }, cancellationToken: cancellationToken));

        return workspaceId;
    }
}
