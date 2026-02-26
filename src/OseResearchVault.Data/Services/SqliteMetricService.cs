using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteMetricService(IAppSettingsService appSettingsService) : IMetricService
{
    public async Task<string> CreateMetricAsync(MetricCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CompanyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MetricName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Period);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SnippetId);

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var snippetContext = await connection.QuerySingleOrDefaultAsync<SnippetContext>(new CommandDefinition(
            @"SELECT s.workspace_id AS WorkspaceId,
                     d.company_id AS CompanyId
                FROM snippet s
                LEFT JOIN document d ON d.id = s.document_id
               WHERE s.id = @SnippetId",
            new { request.SnippetId }, cancellationToken: cancellationToken));

        if (snippetContext is null)
        {
            throw new InvalidOperationException("Snippet was not found.");
        }

        if (!string.IsNullOrWhiteSpace(snippetContext.CompanyId) && !string.Equals(snippetContext.CompanyId, request.CompanyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Metric company must match snippet company.");
        }

        var metricId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO metric (id, workspace_id, company_id, metric_key, metric_value, unit, recorded_at, created_at, snippet_id, period_label, currency)
              VALUES (@Id, @WorkspaceId, @CompanyId, @MetricKey, @MetricValue, @Unit, @RecordedAt, @CreatedAt, @SnippetId, @Period, @Currency)",
            new
            {
                Id = metricId,
                WorkspaceId = snippetContext.WorkspaceId,
                CompanyId = request.CompanyId,
                MetricKey = request.MetricName.Trim(),
                MetricValue = request.Value,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                RecordedAt = now,
                CreatedAt = now,
                SnippetId = request.SnippetId,
                Period = request.Period.Trim(),
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim()
            }, cancellationToken: cancellationToken));

        return metricId;
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());

    private sealed class SnippetContext
    {
        public string WorkspaceId { get; init; } = string.Empty;
        public string? CompanyId { get; init; }
    }
}
