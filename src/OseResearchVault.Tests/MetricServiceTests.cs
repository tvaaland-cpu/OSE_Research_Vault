using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Repositories;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class MetricServiceTests
{
    [Fact]
    public async Task CanCreateMetricLinkedToSnippetAndListByCompany()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ids = await SeedWorkspaceDataAsync(settingsService);
            var service = new MetricService(new SqliteMetricRepository(settingsService));

            var metric = await service.CreateMetricAsync(
                ids.WorkspaceId,
                ids.CompanyId,
                "Net Margin",
                "2024-Q1",
                21.4,
                "percent",
                null,
                ids.SnippetId);

            var companyMetrics = await service.ListMetricsByCompanyAsync(ids.WorkspaceId, ids.CompanyId);
            var namedMetrics = await service.ListMetricsByCompanyAndNameAsync(ids.WorkspaceId, ids.CompanyId, "net margin");

            Assert.Contains(companyMetrics, x => x.MetricId == metric.MetricId && x.SnippetId == ids.SnippetId);
            Assert.Single(namedMetrics);
            Assert.Equal("net_margin", namedMetrics[0].MetricName);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    [Fact]
    public async Task CannotCreateMetricWithoutSnippetId()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ids = await SeedWorkspaceDataAsync(settingsService);
            var service = new MetricService(new SqliteMetricRepository(settingsService));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMetricAsync(
                ids.WorkspaceId,
                ids.CompanyId,
                "Revenue",
                "2024",
                1250,
                "usd_millions",
                "USD",
                string.Empty));

            Assert.Contains("snippet_id", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<(string WorkspaceId, string CompanyId, string DocumentId, string SnippetId)> SeedWorkspaceDataAsync(IAppSettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabaseFilePath,
            ForeignKeys = true
        }.ToString());

        await connection.OpenAsync();

        var workspaceId = Guid.NewGuid().ToString();
        var companyId = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid().ToString();
        var snippetId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync("INSERT INTO workspace (id, name, created_at, updated_at) VALUES (@Id, @Name, @Now, @Now)",
            new { Id = workspaceId, Name = "Test Workspace", Now = now });

        await connection.ExecuteAsync("INSERT INTO company (id, workspace_id, name, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Name, @Now, @Now)",
            new { Id = companyId, WorkspaceId = workspaceId, Name = "Acme Corp", Now = now });

        await connection.ExecuteAsync(
            @"INSERT INTO document (id, workspace_id, company_id, title, document_type, mime_type, file_path, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Title, 'report', 'text/plain', '/tmp/q1.txt', @Now, @Now)",
            new { Id = documentId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "Q1 Letter", Now = now });

        await connection.ExecuteAsync(
            @"INSERT INTO snippet (id, workspace_id, document_id, quote_text, context, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @DocumentId, @QuoteText, @Context, @Now, @Now)",
            new { Id = snippetId, WorkspaceId = workspaceId, DocumentId = documentId, QuoteText = "Evidence quote", Context = "p=1", Now = now });

        return (workspaceId, companyId, documentId, snippetId);
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
