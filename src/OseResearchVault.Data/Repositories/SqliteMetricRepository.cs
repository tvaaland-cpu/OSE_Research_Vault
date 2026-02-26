using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Repositories;

public sealed class SqliteMetricRepository(IAppSettingsService appSettingsService) : IMetricRepository
{
    public async Task<Metric> CreateMetricAsync(string workspaceId, string companyId, string metricName, string period, double value, string? unit, string? currency, string snippetId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var now = DateTime.UtcNow.ToString("O");
        var metricId = Guid.NewGuid().ToString();

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO metric (metric_id, workspace_id, company_id, metric_name, period, value, unit, currency, snippet_id, created_at)
              VALUES (@MetricId, @WorkspaceId, @CompanyId, @MetricName, @Period, @Value, @Unit, @Currency, @SnippetId, @CreatedAt)",
            new
            {
                MetricId = metricId,
                WorkspaceId = workspaceId,
                CompanyId = companyId,
                MetricName = metricName,
                Period = period,
                Value = value,
                Unit = unit,
                Currency = currency,
                SnippetId = snippetId,
                CreatedAt = now
            }, cancellationToken: cancellationToken));

        return new Metric
        {
            MetricId = metricId,
            WorkspaceId = workspaceId,
            CompanyId = companyId,
            MetricName = metricName,
            Period = period,
            Value = value,
            Unit = unit,
            Currency = currency,
            SnippetId = snippetId,
            CreatedAt = now
        };
    }

    public async Task<IReadOnlyList<Metric>> ListMetricsByCompanyAsync(string workspaceId, string companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<Metric>(new CommandDefinition(
            @"SELECT metric_id AS MetricId,
                     workspace_id AS WorkspaceId,
                     company_id AS CompanyId,
                     metric_name AS MetricName,
                     period AS Period,
                     value AS Value,
                     unit AS Unit,
                     currency AS Currency,
                     snippet_id AS SnippetId,
                     created_at AS CreatedAt
                FROM metric
               WHERE workspace_id = @WorkspaceId
                 AND company_id = @CompanyId
            ORDER BY created_at DESC",
            new { WorkspaceId = workspaceId, CompanyId = companyId }, cancellationToken: cancellationToken));

        return results.ToList();
    }

    public async Task<IReadOnlyList<Metric>> ListMetricsByCompanyAndNameAsync(string workspaceId, string companyId, string metricName, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<Metric>(new CommandDefinition(
            @"SELECT metric_id AS MetricId,
                     workspace_id AS WorkspaceId,
                     company_id AS CompanyId,
                     metric_name AS MetricName,
                     period AS Period,
                     value AS Value,
                     unit AS Unit,
                     currency AS Currency,
                     snippet_id AS SnippetId,
                     created_at AS CreatedAt
                FROM metric
               WHERE workspace_id = @WorkspaceId
                 AND company_id = @CompanyId
                 AND metric_name = @MetricName
            ORDER BY created_at DESC",
            new { WorkspaceId = workspaceId, CompanyId = companyId, MetricName = metricName }, cancellationToken: cancellationToken));

        return results.ToList();
    }

    public async Task<Metric?> UpdateMetricAsync(string workspaceId, string metricId, string metricName, string period, double value, string? unit, string? currency, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE metric
                 SET metric_name = @MetricName,
                     period = @Period,
                     value = @Value,
                     unit = @Unit,
                     currency = @Currency
               WHERE workspace_id = @WorkspaceId
                 AND metric_id = @MetricId",
            new
            {
                WorkspaceId = workspaceId,
                MetricId = metricId,
                MetricName = metricName,
                Period = period,
                Value = value,
                Unit = unit,
                Currency = currency
            }, cancellationToken: cancellationToken));

        if (affectedRows == 0)
        {
            return null;
        }

        return await connection.QuerySingleAsync<Metric>(new CommandDefinition(
            @"SELECT metric_id AS MetricId,
                     workspace_id AS WorkspaceId,
                     company_id AS CompanyId,
                     metric_name AS MetricName,
                     period AS Period,
                     value AS Value,
                     unit AS Unit,
                     currency AS Currency,
                     snippet_id AS SnippetId,
                     created_at AS CreatedAt
                FROM metric
               WHERE workspace_id = @WorkspaceId
                 AND metric_id = @MetricId",
            new { WorkspaceId = workspaceId, MetricId = metricId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteMetricAsync(string workspaceId, string metricId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"DELETE FROM metric
               WHERE workspace_id = @WorkspaceId
                 AND metric_id = @MetricId",
            new { WorkspaceId = workspaceId, MetricId = metricId }, cancellationToken: cancellationToken));
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
}
