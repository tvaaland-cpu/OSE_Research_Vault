using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AnnouncementsConnectorTests
{
    [Fact]
    public async Task RunAsync_ManualUrlImport_CreatesEventSourceDocumentAndText()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var fts = new SqliteFtsSyncService(settingsService);
            var connector = new AnnouncementsConnector(settingsService, fts);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = OpenConnection(settings.DatabaseFilePath);
            await connection.OpenAsync();

            var companyId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("O");
            await connection.ExecuteAsync(
                @"INSERT INTO company (id, workspace_id, name, ticker, isin, created_at, updated_at)
                  VALUES (@Id, 'default', 'Acme ASA', 'ACME', 'NO0000000001', @Now, @Now)",
                new { Id = companyId, Now = now });

            var result = await connector.RunAsync(new ConnectorContext
            {
                WorkspaceId = "default",
                CompanyId = companyId,
                HttpClient = new StubHttpClient(),
                Settings = new Dictionary<string, string>
                {
                    ["manual_urls"] = "https://example.test/announcement-1"
                },
                Logger = NullLogger.Instance
            }, CancellationToken.None);

            Assert.Empty(result.Errors);
            Assert.Equal(1, result.SourcesCreated);
            Assert.Equal(1, result.DocumentsCreated);

            var eventCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM event WHERE event_type = 'announcement' AND company_id = @CompanyId", new { CompanyId = companyId });
            var sourceCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM source WHERE company_id = @CompanyId", new { CompanyId = companyId });
            var documentCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM document WHERE company_id = @CompanyId", new { CompanyId = companyId });
            var documentText = await connection.ExecuteScalarAsync<string>(
                @"SELECT dt.content
                    FROM document_text dt
                    JOIN document d ON d.id = dt.document_id
                   WHERE d.company_id = @CompanyId
                   LIMIT 1", new { CompanyId = companyId });

            Assert.Equal(1, eventCount);
            Assert.Equal(1, sourceCount);
            Assert.Equal(1, documentCount);
            Assert.Contains("Known announcement body", documentText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false };
        return new SqliteConnection(builder.ToString());
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

    private sealed class StubHttpClient : IConnectorHttpClient
    {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            if (url.Contains("feeds.finance.yahoo.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult("<rss><channel></channel></rss>");
            }

            return Task.FromResult("<html><head><title>Announcement One</title></head><body><p>Known announcement body for connector testing.</p></body></html>");
        }

        public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    }
}
