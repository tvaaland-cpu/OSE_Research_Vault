using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class CompanyPriceImportTests
{
    [Fact]
    public async Task ImportCompanyDailyPricesCsvAsync_ImportsRows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();
            var companyService = new SqliteCompanyService(settingsService);

            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "Atea" }, []);
            var csvPath = Path.Combine(tempRoot, "prices.csv");
            await File.WriteAllTextAsync(csvPath, "date,close\n2025-01-01,120.5\n2025-01-02,121.7\n");

            var result = await companyService.ImportCompanyDailyPricesCsvAsync(companyId, csvPath);

            Assert.Equal(2, result.InsertedOrUpdatedCount);
            Assert.False(string.IsNullOrWhiteSpace(result.SourceId));
            Assert.False(string.IsNullOrWhiteSpace(result.DocumentId));

            var prices = await companyService.GetCompanyDailyPricesAsync(companyId, 90);
            Assert.Equal(2, prices.Count);
            Assert.Equal("2025-01-02", prices[0].PriceDate);
            Assert.All(prices, price => Assert.Equal(result.SourceId, price.SourceId));

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true, Pooling = false }.ToString());
            await connection.OpenAsync();

            var snapshotPath = await connection.QuerySingleAsync<string>("SELECT file_path FROM document WHERE id = @Id", new { Id = result.DocumentId });
            Assert.True(File.Exists(snapshotPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImportCompanyDailyPricesCsvAsync_UpsertsDuplicateDate()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();
            var companyService = new SqliteCompanyService(settingsService);

            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "Atea" }, []);
            var csv1 = Path.Combine(tempRoot, "prices-1.csv");
            var csv2 = Path.Combine(tempRoot, "prices-2.csv");
            await File.WriteAllTextAsync(csv1, "date,close\n2025-01-01,120.5\n");
            await File.WriteAllTextAsync(csv2, "date,close\n2025-01-01,122.0\n");

            await companyService.ImportCompanyDailyPricesCsvAsync(companyId, csv1);
            var secondImport = await companyService.ImportCompanyDailyPricesCsvAsync(companyId, csv2);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true, Pooling = false }.ToString());
            await connection.OpenAsync();

            var count = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM price_daily WHERE company_id = @CompanyId", new { CompanyId = companyId });
            Assert.Equal(1, count);

            var latest = await companyService.GetLatestCompanyPriceAsync(companyId);
            Assert.NotNull(latest);
            Assert.Equal(122.0, latest!.Close);
            Assert.Equal(secondImport.SourceId, latest.SourceId);
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
