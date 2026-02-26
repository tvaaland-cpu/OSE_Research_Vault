using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteMetricService(IAppSettingsService appSettingsService) : IMetricService
{
    public async Task<MetricUpsertResult> UpsertMetricAsync(MetricUpsertRequest request, MetricConflictResolution conflictResolution = MetricConflictResolution.CreateOnly, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyId))
        {
            throw new InvalidOperationException("Company is required.");
        }

        var normalizedMetricName = NormalizeMetricName(request.MetricName);
        if (string.IsNullOrWhiteSpace(normalizedMetricName))
        {
            throw new InvalidOperationException("Metric name is required.");
        }

        var period = request.Period.Trim();
        if (string.IsNullOrWhiteSpace(period))
        {
            throw new InvalidOperationException("Metric period is required.");
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var existingId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            @"SELECT id
                FROM metric
               WHERE company_id = @CompanyId
                 AND metric_key = @MetricName
                 AND COALESCE(period_end, '') = @Period
               ORDER BY created_at DESC
               LIMIT 1",
            new
            {
                request.CompanyId,
                MetricName = normalizedMetricName,
                Period = period
            }, cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            if (conflictResolution == MetricConflictResolution.CreateOnly)
            {
                return new MetricUpsertResult
                {
                    Status = MetricUpsertStatus.ConflictDetected,
                    MetricId = existingId,
                    NormalizedMetricName = normalizedMetricName
                };
            }

            if (conflictResolution == MetricConflictResolution.ReplaceExisting)
            {
                // Replace uses UPDATE to preserve metric id and any future immutable associations that may reference this metric row.
                await connection.ExecuteAsync(new CommandDefinition(
                    @"UPDATE metric
                         SET metric_value = @MetricValue,
                             unit = @Unit,
                             recorded_at = @RecordedAt
                       WHERE id = @Id",
                    new
                    {
                        Id = existingId,
                        MetricValue = request.Value,
                        Unit = BuildUnit(request.Unit, request.Currency),
                        RecordedAt = DateTime.UtcNow.ToString("O")
                    }, cancellationToken: cancellationToken));

                return new MetricUpsertResult
                {
                    Status = MetricUpsertStatus.Replaced,
                    MetricId = existingId,
                    NormalizedMetricName = normalizedMetricName
                };
            }
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
            @"INSERT INTO metric (id, workspace_id, company_id, metric_key, metric_value, unit, period_end, recorded_at, created_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @MetricName, @MetricValue, @Unit, @Period, @RecordedAt, @CreatedAt)",
            new
            {
                Id = metricId,
                WorkspaceId = workspaceId,
                request.CompanyId,
                MetricName = normalizedMetricName,
                MetricValue = request.Value,
                Unit = BuildUnit(request.Unit, request.Currency),
                Period = period,
                RecordedAt = now,
                CreatedAt = now
            }, cancellationToken: cancellationToken));

        return new MetricUpsertResult
        {
            Status = conflictResolution == MetricConflictResolution.CreateAnyway ? MetricUpsertStatus.CreatedAnyway : MetricUpsertStatus.Created,
            MetricId = metricId,
            NormalizedMetricName = normalizedMetricName
        };
    }

    internal static string NormalizeMetricName(string metricName)
    {
        return metricName.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string? BuildUnit(string? unit, string? currency)
    {
        var cleanedUnit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        var cleanedCurrency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();

        return cleanedCurrency switch
        {
            null => cleanedUnit,
            _ when string.IsNullOrWhiteSpace(cleanedUnit) => cleanedCurrency,
            _ => $"{cleanedUnit} ({cleanedCurrency})"
        };
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
    }

    private static async Task<string> EnsureWorkspaceAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync(cancellationToken);

        var workspaceId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM workspace ORDER BY created_at LIMIT 1", cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaceId;
        }

        workspaceId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO workspace (id, name, base_currency, created_at, updated_at) VALUES (@Id, @Name, @Currency, @Now, @Now)",
            new { Id = workspaceId, Name = "Default Workspace", Currency = "USD", Now = DateTime.UtcNow.ToString("O") }, cancellationToken: cancellationToken));

        return workspaceId;
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
