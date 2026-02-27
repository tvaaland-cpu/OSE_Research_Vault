using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Repositories;

namespace OseResearchVault.Data.Services;

public sealed class SqliteMetricService : IMetricService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly MetricService _legacyMetricService;

    public SqliteMetricService(IAppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
        _legacyMetricService = new MetricService(new SqliteMetricRepository(appSettingsService));
    }

    public async Task<MetricUpsertResult> UpsertMetricAsync(MetricUpsertRequest request, MetricConflictResolution conflictResolution = MetricConflictResolution.CreateOnly, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CompanyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MetricName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Period);

        var normalizedMetricName = NormalizeMetricName(request.MetricName);
        var period = request.Period.Trim();

        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        var snippetId = await EnsureSystemSnippetAsync(connection, workspaceId, request.CompanyId.Trim(), cancellationToken);

        var existingId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            @"SELECT metric_id
                FROM metric
               WHERE workspace_id = @WorkspaceId
                 AND company_id = @CompanyId
                 AND metric_name = @MetricName
                 AND COALESCE(period, '') = @Period
               ORDER BY created_at DESC
               LIMIT 1",
            new
            {
                WorkspaceId = workspaceId,
                CompanyId = request.CompanyId.Trim(),
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
                await connection.ExecuteAsync(new CommandDefinition(
                    @"UPDATE metric
                         SET value = @Value,
                             unit = @Unit,
                             currency = @Currency,
                             created_at = @Now
                       WHERE metric_id = @MetricId",
                    new
                    {
                        MetricId = existingId,
                        Value = request.Value,
                        Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                        Currency = CleanCurrency(request.Currency),
                        Now = DateTime.UtcNow.ToString("O")
                    }, cancellationToken: cancellationToken));

                return new MetricUpsertResult
                {
                    Status = MetricUpsertStatus.Replaced,
                    MetricId = existingId,
                    NormalizedMetricName = normalizedMetricName
                };
            }
        }

        var metricId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO metric (metric_id, workspace_id, company_id, metric_name, period, value, unit, currency, snippet_id, created_at)
              VALUES (@MetricId, @WorkspaceId, @CompanyId, @MetricName, @Period, @Value, @Unit, @Currency, @SnippetId, @CreatedAt)",
            new
            {
                MetricId = metricId,
                WorkspaceId = workspaceId,
                CompanyId = request.CompanyId.Trim(),
                MetricName = normalizedMetricName,
                Period = period,
                Value = request.Value,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                Currency = CleanCurrency(request.Currency),
                SnippetId = snippetId,
                CreatedAt = now
            }, cancellationToken: cancellationToken));

        return new MetricUpsertResult
        {
            Status = conflictResolution == MetricConflictResolution.CreateAnyway ? MetricUpsertStatus.CreatedAnyway : MetricUpsertStatus.Created,
            MetricId = metricId,
            NormalizedMetricName = normalizedMetricName
        };
    }

    public async Task<string> CreateMetricAsync(MetricCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CompanyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MetricName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Period);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SnippetId);

        var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
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

        if (!string.IsNullOrWhiteSpace(snippetContext.CompanyId) &&
            !string.Equals(snippetContext.CompanyId, request.CompanyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Metric company must match snippet company.");
        }

        var metricId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO metric (metric_id, workspace_id, company_id, metric_name, period, value, unit, currency, snippet_id, created_at)
              VALUES (@MetricId, @WorkspaceId, @CompanyId, @MetricName, @Period, @Value, @Unit, @Currency, @SnippetId, @CreatedAt)",
            new
            {
                MetricId = metricId,
                WorkspaceId = snippetContext.WorkspaceId,
                CompanyId = request.CompanyId.Trim(),
                MetricName = NormalizeMetricName(request.MetricName),
                Period = request.Period.Trim(),
                Value = request.Value,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                CreatedAt = now,
                SnippetId = request.SnippetId.Trim(),
                Currency = CleanCurrency(request.Currency)
            }, cancellationToken: cancellationToken));

        return metricId;
    }

    public Task<Metric> CreateMetricAsync(string workspaceId, string companyId, string metricName, string period, double value, string? unit, string? currency, string snippetId, CancellationToken cancellationToken = default)
        => _legacyMetricService.CreateMetricAsync(workspaceId, companyId, metricName, period, value, unit, currency, snippetId, cancellationToken);

    public Task<IReadOnlyList<Metric>> ListMetricsByCompanyAsync(string workspaceId, string companyId, CancellationToken cancellationToken = default)
        => _legacyMetricService.ListMetricsByCompanyAsync(workspaceId, companyId, cancellationToken);

    public Task<IReadOnlyList<Metric>> ListMetricsByCompanyAndNameAsync(string workspaceId, string companyId, string metricName, CancellationToken cancellationToken = default)
        => _legacyMetricService.ListMetricsByCompanyAndNameAsync(workspaceId, companyId, metricName, cancellationToken);

    public Task<Metric?> UpdateMetricAsync(string workspaceId, string metricId, string metricName, string period, double value, string? unit, string? currency, CancellationToken cancellationToken = default)
        => _legacyMetricService.UpdateMetricAsync(workspaceId, metricId, metricName, period, value, unit, currency, cancellationToken);

    public Task DeleteMetricAsync(string workspaceId, string metricId, CancellationToken cancellationToken = default)
        => _legacyMetricService.DeleteMetricAsync(workspaceId, metricId, cancellationToken);

    internal static string NormalizeMetricName(string metricName)
        => metricName.Trim().ToLowerInvariant().Replace(' ', '_');

    private static string? CleanCurrency(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static async Task<string> EnsureSystemSnippetAsync(SqliteConnection connection, string workspaceId, string companyId, CancellationToken cancellationToken)
    {
        var existing = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            @"SELECT id
                FROM snippet
               WHERE workspace_id = @WorkspaceId
                 AND quote_text = @QuoteText
               LIMIT 1",
            new { WorkspaceId = workspaceId, QuoteText = "System metric upsert." }, cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var snippetId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO snippet (id, workspace_id, document_id, note_id, source_id, quote_text, context, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, NULL, NULL, NULL, @QuoteText, 'system', @Now, @Now)",
            new { Id = snippetId, WorkspaceId = workspaceId, CompanyId = companyId, QuoteText = "System metric upsert.", Now = now }, cancellationToken: cancellationToken));

        return snippetId;
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString());

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
        var now = DateTime.UtcNow.ToString("O");
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO workspace (id, name, created_at, updated_at) VALUES (@Id, @Name, @Now, @Now)",
            new { Id = workspaceId, Name = "Default Workspace", Now = now }, cancellationToken: cancellationToken));

        return workspaceId;
    }

    private sealed class SnippetContext
    {
        public string WorkspaceId { get; init; } = string.Empty;
        public string? CompanyId { get; init; }
    }
}
