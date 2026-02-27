using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class CompanyMetricsServiceTests
{
    [Fact]
    public async Task CanReadFilterUpdateAndDeleteCompanyMetrics()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var companyService = new SqliteCompanyService(settingsService);
            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "MetricCo" }, []);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true, Pooling = false }.ToString());
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT workspace_id FROM company WHERE id = @CompanyId", new { CompanyId = companyId });
            var sourceId = Guid.NewGuid().ToString();
            var documentId = Guid.NewGuid().ToString();
            var snippetId = Guid.NewGuid().ToString();
            var metricId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("O");

            await connection.ExecuteAsync(
                "INSERT INTO source (id, workspace_id, company_id, name, source_type, created_at, updated_at) VALUES (@Id, @WorkspaceId, @CompanyId, 'Earnings Call', 'transcript', @Now, @Now)",
                new { Id = sourceId, WorkspaceId = workspaceId, CompanyId = companyId, Now = now });
            await connection.ExecuteAsync(
                "INSERT INTO document (id, workspace_id, company_id, source_id, title, created_at, updated_at) VALUES (@Id, @WorkspaceId, @CompanyId, @SourceId, 'Q1 Report', @Now, @Now)",
                new { Id = documentId, WorkspaceId = workspaceId, CompanyId = companyId, SourceId = sourceId, Now = now });
            await connection.ExecuteAsync(
                "INSERT INTO snippet (id, workspace_id, document_id, source_id, quote_text, context, created_at, updated_at) VALUES (@Id, @WorkspaceId, @DocumentId, @SourceId, 'Revenue reached 123', 'p. 5', @Now, @Now)",
                new { Id = snippetId, WorkspaceId = workspaceId, DocumentId = documentId, SourceId = sourceId, Now = now });
            await connection.ExecuteAsync(
                @"INSERT INTO metric (metric_id, workspace_id, company_id, metric_name, period, value, unit, currency, snippet_id, created_at)
                  VALUES (@Id, @WorkspaceId, @CompanyId, 'Revenue', '2024-Q1', 123, 'MNOK', 'NOK', @SnippetId, @Now)",
                new { Id = metricId, WorkspaceId = workspaceId, CompanyId = companyId, SnippetId = snippetId, Now = now });

            var metrics = await companyService.GetCompanyMetricsAsync(companyId);
            var metric = Assert.Single(metrics);
            Assert.Equal("Revenue", metric.MetricName);
            Assert.Equal("Q1 Report", metric.DocumentTitle);
            Assert.Equal("p. 5", metric.Locator);

            var names = await companyService.GetCompanyMetricNamesAsync(companyId);
            Assert.Single(names);
            Assert.Contains("Revenue", names[0], StringComparison.Ordinal);

            await companyService.UpdateCompanyMetricAsync(metricId, new CompanyMetricUpdateRequest
            {
                MetricName = "Adj Revenue",
                Period = "2024-Q2",
                Value = 140,
                Unit = "MNOK",
                Currency = "NOK"
            });

            var updated = Assert.Single(await companyService.GetCompanyMetricsAsync(companyId));
            Assert.Equal("Adj Revenue", updated.MetricName);
            Assert.Equal(140d, updated.Value);
            Assert.Equal(snippetId, updated.SnippetId);

            await companyService.DeleteCompanyMetricAsync(metricId);
            Assert.Empty(await companyService.GetCompanyMetricsAsync(companyId));
        }
        finally
        {
            TestCleanup.DeleteDirectory(tempRoot);
        }
    }

    private sealed class TestAppSettingsService(string rootDirectory) : IAppSettingsService
    {
        private readonly AppSettings _settings = new()
        {
            DatabaseDirectory = Path.Combine(rootDirectory, "db"),
            VaultStorageDirectory = Path.Combine(rootDirectory, "vault")
        };

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_settings.DatabaseDirectory);
            Directory.CreateDirectory(_settings.VaultStorageDirectory);
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
