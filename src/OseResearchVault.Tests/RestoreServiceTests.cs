using System.IO.Compression;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class RestoreServiceTests
{
    [Fact]
    public async Task RestoreWorkspaceFromZip_CreatesWorkspaceFilesAndSettingsEntry()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-restore-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var zipPath = Path.Combine(tempRoot, "backup.zip");
            await CreateBackupFixtureAsync(zipPath);

            var settings = new AppSettings
            {
                DatabaseDirectory = Path.Combine(tempRoot, "default", "data"),
                VaultStorageDirectory = Path.Combine(tempRoot, "default", "vault"),
                ImportInboxFolderPath = Path.Combine(tempRoot, "inbox")
            };

            var settingsService = new InMemoryAppSettingsService(settings);
            var service = new SqliteRestoreService(settingsService);

            var destination = Path.Combine(tempRoot, "restored-workspace");
            var restored = await service.RestoreWorkspaceFromZipAsync(zipPath, destination, "Renamed Workspace");

            Assert.Equal("Renamed Workspace", restored.Name);
            Assert.True(File.Exists(Path.Combine(destination, "data", "ose-research-vault.db")));
            Assert.True(File.Exists(Path.Combine(destination, "vault", "docs", "sample.txt")));

            var persisted = await settingsService.GetSettingsAsync();
            Assert.Contains(persisted.Workspaces, w => w.Id == restored.Id && w.Path == destination && w.Name == "Renamed Workspace");
        }
        finally
        {
            TestCleanup.DeleteDirectory(tempRoot);
        }
    }

    private static async Task CreateBackupFixtureAsync(string zipPath)
    {
        var buildRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-restore-fixture", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(buildRoot);

        try
        {
            var dbPath = Path.Combine(buildRoot, "fixture.db");
            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync("CREATE TABLE workspace (id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);");
                await connection.ExecuteAsync("INSERT INTO workspace (id, name, description, created_at, updated_at) VALUES (@Id, @Name, @Description, @Now, @Now)",
                    new { Id = "workspace-1", Name = "Workspace From Backup", Description = "fixture", Now = DateTimeOffset.UtcNow.ToString("O") });
            }

            SqliteConnection.ClearAllPools();

            var vaultFile = Path.Combine(buildRoot, "sample.txt");
            await File.WriteAllTextAsync(vaultFile, "restored content");

            var manifestPath = Path.Combine(buildRoot, "manifest.json");
            await File.WriteAllTextAsync(manifestPath, """
{
  "workspace_id": "workspace-1",
  "workspace_name": "Workspace From Backup"
}
""");

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(manifestPath, "manifest.json");
            archive.CreateEntryFromFile(dbPath, "database/ose-research-vault.db");
            archive.CreateEntryFromFile(vaultFile, "vault/docs/sample.txt");
        }
        finally
        {
            TestCleanup.DeleteDirectory(buildRoot);
        }
    }

    private sealed class InMemoryAppSettingsService(AppSettings settings) : IAppSettingsService
    {
        private AppSettings _settings = settings;

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }
}
