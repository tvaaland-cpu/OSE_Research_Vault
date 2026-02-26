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
            @"SELECT id,
                     name,
                     enabled = 1 AS Enabled,
                     schedule_type AS ScheduleType,
                     interval_minutes AS IntervalMinutes,
                     daily_time AS DailyTime,
                     payload_type AS PayloadType,
                     agent_id AS AgentId,
                     company_scope_mode AS CompanyScopeMode,
                     COALESCE(company_scope_ids_json, '[]') AS CompanyScopeIdsJson,
                     COALESCE(query_text, '') AS QueryText,
                     next_run_at AS NextRunAt,
                     last_run_at AS LastRunAt,
                     last_status AS LastStatus
                FROM automation
            ORDER BY created_at DESC", cancellationToken: cancellationToken));

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
            @"INSERT INTO automation (id, workspace_id, name, enabled, schedule_type, interval_minutes, daily_time, payload_type, agent_id, company_scope_mode, company_scope_ids_json, query_text, next_run_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @Name, @Enabled, @ScheduleType, @IntervalMinutes, @DailyTime, @PayloadType, @AgentId, @CompanyScopeMode, @CompanyScopeIdsJson, @QueryText, @NextRunAt, @Now, @Now)",
            new
            {
                Id = automationId,
                WorkspaceId = workspaceId,
                Name = request.Name.Trim(),
                Enabled = request.Enabled ? 1 : 0,
                ScheduleType = request.ScheduleType,
                request.IntervalMinutes,
                DailyTime = Clean(request.DailyTime),
                PayloadType = request.PayloadType,
                AgentId = Clean(request.AgentId),
                CompanyScopeMode = request.CompanyScopeMode,
                CompanyScopeIdsJson = JsonSerializer.Serialize(request.CompanyScopeIds.Distinct()),
                QueryText = request.QueryText.Trim(),
                NextRunAt = request.Enabled ? nextRun : null,
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
                     enabled = @Enabled,
                     schedule_type = @ScheduleType,
                     interval_minutes = @IntervalMinutes,
                     daily_time = @DailyTime,
                     payload_type = @PayloadType,
                     agent_id = @AgentId,
                     company_scope_mode = @CompanyScopeMode,
                     company_scope_ids_json = @CompanyScopeIdsJson,
                     query_text = @QueryText,
                     next_run_at = @NextRunAt,
                     updated_at = @Now
               WHERE id = @Id",
            new
            {
                Id = automationId,
                Name = request.Name.Trim(),
                Enabled = request.Enabled ? 1 : 0,
                ScheduleType = request.ScheduleType,
                request.IntervalMinutes,
                DailyTime = Clean(request.DailyTime),
                PayloadType = request.PayloadType,
                AgentId = Clean(request.AgentId),
                CompanyScopeMode = request.CompanyScopeMode,
                CompanyScopeIdsJson = JsonSerializer.Serialize(request.CompanyScopeIds.Distinct()),
                QueryText = request.QueryText.Trim(),
                NextRunAt = request.Enabled ? nextRun : null,
                Now = now.ToString("O")
            }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAutomationAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM automation WHERE id = @Id", new { Id = automationId }, cancellationToken: cancellationToken));
    }

    public async Task SetAutomationEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var schedule = await connection.QuerySingleOrDefaultAsync<(string ScheduleType, int? IntervalMinutes, string? DailyTime)>(new CommandDefinition(
            "SELECT schedule_type as ScheduleType, interval_minutes as IntervalMinutes, daily_time as DailyTime FROM automation WHERE id = @Id",
            new { Id = automationId }, cancellationToken: cancellationToken));

        var now = DateTime.UtcNow;
        var nextRunAt = enabled
            ? ComputeNextRunAt(new AutomationUpsertRequest { ScheduleType = schedule.ScheduleType, IntervalMinutes = schedule.IntervalMinutes, DailyTime = schedule.DailyTime }, now)
            : null;

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE automation
                 SET enabled = @Enabled,
                     next_run_at = @NextRunAt,
                     updated_at = @Now
               WHERE id = @Id",
            new { Id = automationId, Enabled = enabled ? 1 : 0, NextRunAt = nextRunAt, Now = now.ToString("O") }, cancellationToken: cancellationToken));
    }

    public async Task<string> RunNowAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var runId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO automation_run (id, automation_id, trigger_type, status, started_at, finished_at)
              VALUES (@Id, @AutomationId, 'manual', 'success', @Now, @Now)",
            new { Id = runId, AutomationId = automationId, Now = now.ToString("O") }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE automation
                 SET last_run_at = @Now,
                     last_status = 'success',
                     next_run_at = CASE
                        WHEN enabled = 0 THEN NULL
                        WHEN schedule_type = 'daily' THEN CASE
                            WHEN time(@Now) <= time(daily_time || ':00') THEN strftime('%Y-%m-%dT', @Now) || daily_time || ':00.0000000Z'
                            ELSE strftime('%Y-%m-%dT', datetime(@Now, '+1 day')) || daily_time || ':00.0000000Z'
                        END
                        ELSE datetime(@Now, '+' || COALESCE(interval_minutes, 60) || ' minutes')
                     END,
                     updated_at = @Now
               WHERE id = @AutomationId",
            new { AutomationId = automationId, Now = now.ToString("O") }, cancellationToken: cancellationToken));

        return runId;
    }

    public async Task<IReadOnlyList<AutomationRunRecord>> GetRunsAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AutomationRunRecord>(new CommandDefinition(
            @"SELECT id,
                     automation_id AS AutomationId,
                     trigger_type AS TriggerType,
                     status,
                     started_at AS StartedAt,
                     finished_at AS FinishedAt
                FROM automation_run
               WHERE automation_id = @AutomationId
            ORDER BY started_at DESC",
            new { AutomationId = automationId }, cancellationToken: cancellationToken));

        return rows.ToList();
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
            ForeignKeys = true
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

        var workspaceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO workspace (id, name, description, created_at, updated_at) VALUES (@Id, @Name, @Description, @Now, @Now)",
            new { Id = workspaceId, Name = "Default Workspace", Description = "Created automatically", Now = now }, cancellationToken: cancellationToken));

        return workspaceId;
    }
}
