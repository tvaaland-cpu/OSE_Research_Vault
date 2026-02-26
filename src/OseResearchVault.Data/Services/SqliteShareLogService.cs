using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteShareLogService(IAppSettingsService appSettingsService) : IShareLogService
{
    public async Task AddAsync(ShareLogCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO share_log (share_log_id, workspace_id, action, target_company_id, profile_id, output_path, created_at, summary)
              VALUES (@ShareLogId, @WorkspaceId, @Action, @TargetCompanyId, @ProfileId, @OutputPath, @CreatedAt, @Summary)",
            new
            {
                ShareLogId = Guid.NewGuid().ToString(),
                WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId,
                request.Action,
                request.TargetCompanyId,
                request.ProfileId,
                request.OutputPath,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                request.Summary
            }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ShareLogRecord>> GetRecentAsync(string workspaceId, int limit = 200, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}");
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ShareLogRecord>(new CommandDefinition(
            @"SELECT sl.share_log_id AS ShareLogId,
                     COALESCE(sl.workspace_id, '') AS WorkspaceId,
                     sl.action AS Action,
                     sl.target_company_id AS TargetCompanyId,
                     c.name AS TargetCompanyName,
                     sl.profile_id AS ProfileId,
                     ep.name AS ProfileName,
                     sl.output_path AS OutputPath,
                     sl.created_at AS CreatedAt,
                     sl.summary AS Summary
                FROM share_log sl
                LEFT JOIN company c ON c.id = sl.target_company_id
                LEFT JOIN export_profile ep ON ep.profile_id = sl.profile_id
               WHERE @WorkspaceId = '' OR COALESCE(sl.workspace_id, '') = @WorkspaceId
            ORDER BY sl.created_at DESC
               LIMIT @Limit",
            new { WorkspaceId = workspaceId ?? string.Empty, Limit = Math.Max(1, limit) }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
