using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class MetricServiceTests
{
    [Fact]
    public async Task CreatingSameMetricTwiceReturnsConflict()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var companyService = new SqliteCompanyService(settingsService);
            var metricService = new SqliteMetricService(settingsService);

            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "Metric Co" }, []);
            var request = new MetricUpsertRequest
            {
                CompanyId = companyId,
                MetricName = " Revenue Growth ",
                Period = "2025-Q1",
                Value = 11.2,
                Unit = "%",
                Currency = "usd"
            };

            var first = await metricService.UpsertMetricAsync(request);
            var second = await metricService.UpsertMetricAsync(request);

            Assert.Equal(MetricUpsertStatus.Created, first.Status);
            Assert.Equal("revenue_growth", first.NormalizedMetricName);
            Assert.Equal(MetricUpsertStatus.ConflictDetected, second.Status);
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
