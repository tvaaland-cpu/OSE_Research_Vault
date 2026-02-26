using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class OseDirectoryCsvConnectorTests
{
    [Fact]
    public async Task RunAsync_ImportsAndUpdatesCompanies_AndCreatesSourceDocumentSnapshot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var connector = new OseDirectoryCsvConnector(settingsService, ftsSyncService);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = OpenConnection(settings.DatabaseFilePath);
            await connection.OpenAsync();

            var now = DateTime.UtcNow.ToString("O");
            var existingCompanyId = Guid.NewGuid().ToString();
            await connection.ExecuteAsync(
                @"INSERT INTO company (id, workspace_id, name, ticker, isin, summary, created_at, updated_at)
                  VALUES (@Id, 'default', 'Aker Solutions ASA', 'AKSO', NULL, 'Custom analyst summary', @Now, @Now)",
                new { Id = existingCompanyId, Now = now });

            var csvPath = Path.Combine(AppContext.BaseDirectory, "TestData", "ose-directory-sample.csv");
            Assert.True(File.Exists(csvPath));

            var result = await connector.RunAsync(new ConnectorContext
            {
                WorkspaceId = "default",
                HttpClient = new NoopHttpClient(),
                Settings = new Dictionary<string, string> { ["csv_path"] = csvPath },
                Logger = NullLogger.Instance
            }, CancellationToken.None);

            Assert.Empty(result.Errors);
            Assert.Equal(1, result.SourcesCreated);
            Assert.Equal(1, result.DocumentsCreated);

            var companies = (await connection.QueryAsync<(string name, string? ticker, string? isin, string? summary)>(
                "SELECT name, ticker, isin, summary FROM company ORDER BY name")).ToList();
            Assert.Equal(2, companies.Count);

            var aker = companies.Single(c => c.name == "Aker Solutions ASA");
            Assert.Equal("AKSO", aker.ticker);
            Assert.Equal("NO0010716582", aker.isin);
            Assert.Equal("Custom analyst summary", aker.summary);

            var wawi = companies.Single(c => c.name == "Wallenius Wilhelmsen ASA");
            Assert.Equal("WAWI", wawi.ticker);
            Assert.Equal("NO0010571680", wawi.isin);
            Assert.Contains("Oslo Stock Exchange", wawi.summary);

            var sourceCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM source WHERE source_type = 'import'");
            var documentCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM document WHERE doc_type = 'csv'");
            Assert.Equal(1, sourceCount);
            Assert.Equal(1, documentCount);
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
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true };
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

    private sealed class NoopHttpClient : IConnectorHttpClient
    {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

        public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    }
}
