using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class MetricServiceTests
{
    [Fact]
    public async Task CreateMetricFromSnippet_PersistsEvidenceColumns()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var companyService = new SqliteCompanyService(settingsService);
            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var documentService = new SqliteDocumentImportService(settingsService, ftsSyncService, NullLogger<SqliteDocumentImportService>.Instance);
            var snippetRepository = new SqliteSnippetRepository(settingsService);
            var evidenceService = new EvidenceService(snippetRepository, new SqliteEvidenceLinkRepository(settingsService));
            var metricService = new SqliteMetricService(settingsService);

            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "Atea", Currency = "NOK" }, []);

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);
            var txtPath = Path.Combine(inputDirectory, "atea.txt");
            await File.WriteAllTextAsync(txtPath, "Revenue rose to 200.");

            var importResults = await documentService.ImportFilesAsync([txtPath]);
            Assert.True(importResults.Single().Succeeded);
            var document = (await documentService.GetDocumentsAsync()).Single();
            await documentService.UpdateDocumentCompanyAsync(document.Id, companyId);

            var snippet = await evidenceService.CreateSnippetAsync(document.WorkspaceId, document.Id, companyId, null, "p.1", "Revenue rose to 200.", "user");

            await metricService.CreateMetricAsync(new MetricCreateRequest
            {
                CompanyId = companyId,
                MetricName = "revenue",
                Period = "2025Q4",
                Value = 200,
                Unit = "NOKm",
                Currency = "NOK",
                SnippetId = snippet.Id
            });

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var metric = await connection.QuerySingleAsync<(string metric_key, string period_label, string currency, string snippet_id)>(
                "SELECT metric_key, period_label, currency, snippet_id FROM metric LIMIT 1");

            Assert.Equal("revenue", metric.metric_key);
            Assert.Equal("2025Q4", metric.period_label);
            Assert.Equal("NOK", metric.currency);
            Assert.Equal(snippet.Id, metric.snippet_id);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
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
