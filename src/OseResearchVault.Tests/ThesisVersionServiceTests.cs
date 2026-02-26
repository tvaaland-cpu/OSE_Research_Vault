using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class ThesisVersionServiceTests
{
    [Fact]
    public async Task CreatingTwoVersions_ReturnsLatestVersionFirst()
    {
        var (root, settings, thesisService, connection) = await CreateFixtureAsync();

        try
        {
            var companyId = await InsertCompanyAsync(connection, "Test Co");

            await thesisService.CreateThesisVersionAsync(new CreateThesisVersionRequest
            {
                CompanyId = companyId,
                Title = "Thesis",
                Body = "Version 1"
            });

            await Task.Delay(20);

            await thesisService.CreateThesisVersionAsync(new CreateThesisVersionRequest
            {
                CompanyId = companyId,
                Title = "Thesis",
                Body = "Version 2"
            });

            var latest = await thesisService.GetLatestThesisVersionAsync(companyId);

            Assert.NotNull(latest);
            Assert.Equal("Version 2", latest!.Body);
        }
        finally
        {
            await connection.DisposeAsync();
            Cleanup(root);
        }
    }

    [Fact]
    public async Task CreatingNewVersion_DoesNotMutatePreviousVersions()
    {
        var (root, settings, thesisService, connection) = await CreateFixtureAsync();

        try
        {
            var companyId = await InsertCompanyAsync(connection, "Append Co");

            await thesisService.CreateThesisVersionAsync(new CreateThesisVersionRequest
            {
                CompanyId = companyId,
                Body = "Initial thesis body"
            });

            await thesisService.CreateThesisVersionAsync(new CreateThesisVersionRequest
            {
                CompanyId = companyId,
                Body = "Updated thesis body"
            });

            var versions = await thesisService.GetThesisVersionsAsync(companyId);

            Assert.Equal(2, versions.Count);
            Assert.Contains(versions, v => v.Body == "Initial thesis body");
            Assert.Contains(versions, v => v.Body == "Updated thesis body");
        }
        finally
        {
            await connection.DisposeAsync();
            Cleanup(root);
        }
    }

    private static async Task<(string Root, IAppSettingsService Settings, IThesisService Service, SqliteConnection Connection)> CreateFixtureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var settings = new TestAppSettingsService(root);
        var initializer = new SqliteDatabaseInitializer(settings, NullLogger<SqliteDatabaseInitializer>.Instance);
        await initializer.InitializeAsync();

        var appSettings = await settings.GetSettingsAsync();
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = appSettings.DatabaseFilePath,
            ForeignKeys = true
        }.ToString());
        await connection.OpenAsync();

        var service = new SqliteThesisService(settings);
        return (root, settings, service, connection);
    }

    private static async Task<string> InsertCompanyAsync(SqliteConnection connection, string name)
    {
        var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
        var companyId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(
            @"INSERT INTO company (id, workspace_id, name, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @Name, @Now, @Now)",
            new { Id = companyId, WorkspaceId = workspaceId, Name = name, Now = now });

        return companyId;
    }

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestAppSettingsService(string rootDirectory) : IAppSettingsService
    {
        private readonly AppSettings _settings = new()
        {
            DatabaseDirectory = rootDirectory,
            VaultStorageDirectory = rootDirectory
        };

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
