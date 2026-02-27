using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteNotificationService(IAppSettingsService appSettingsService) : INotificationService
{
    public async Task AddNotification(string level, string title, string body, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO notification (notification_id, workspace_id, level, title, body, created_at, is_read)
              VALUES (@NotificationId, @WorkspaceId, @Level, @Title, @Body, @CreatedAt, 0)",
            new
            {
                NotificationId = Guid.NewGuid().ToString(),
                WorkspaceId = workspaceId,
                Level = NormalizeLevel(level),
                Title = title.Trim(),
                Body = body.Trim(),
                CreatedAt = DateTime.UtcNow.ToString("O")
            }, cancellationToken: cancellationToken));
    }

    public async Task MarkRead(string notificationId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE notification
                 SET is_read = 1
               WHERE notification_id = @NotificationId",
            new { NotificationId = notificationId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<NotificationRecord>> ListNotifications(string workspaceId, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var targetWorkspaceId = string.IsNullOrWhiteSpace(workspaceId)
            ? await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken)
            : workspaceId;

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<NotificationRecord>(new CommandDefinition(
            @"SELECT notification_id AS NotificationId,
                     workspace_id AS WorkspaceId,
                     level,
                     COALESCE(title, '') AS Title,
                     COALESCE(body, '') AS Body,
                     created_at AS CreatedAt,
                     CASE WHEN is_read = 1 THEN 1 ELSE 0 END AS IsRead
                FROM notification
               WHERE workspace_id = @WorkspaceId
                 AND (@UnreadOnly = 0 OR is_read = 0)
            ORDER BY created_at DESC",
            new { WorkspaceId = targetWorkspaceId, UnreadOnly = unreadOnly ? 1 : 0 }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static string NormalizeLevel(string level)
    {
        if (string.Equals(level, "warn", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(level, "error", StringComparison.OrdinalIgnoreCase))
        {
            return level.ToLowerInvariant();
        }

        return "info";
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false };
        return new SqliteConnection(builder.ToString());
    }

    private static async Task<string> EnsureWorkspaceAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync(cancellationToken);

        var existing = await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM workspace ORDER BY created_at LIMIT 1", cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var now = DateTime.UtcNow.ToString("O");
        var workspaceId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO workspace (id, name, created_at)
              VALUES (@Id, 'Default Workspace', @Now)",
            new { Id = workspaceId, Now = now }, cancellationToken: cancellationToken));

        return workspaceId;
    }
}
