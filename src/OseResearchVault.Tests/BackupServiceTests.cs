using System.IO.Compression;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task ExportWorkspaceBackup_CreatesZipWithManifestDatabaseAndVaultFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            var vaultFile = Path.Combine(settings.VaultStorageDirectory, "folder", "sample.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(vaultFile)!);
            await File.WriteAllTextAsync(vaultFile, "vault-file");

            await using (var connection = new SqliteConnection($"Data Source={settings.DatabaseFilePath}"))
            {
                await connection.OpenAsync();
                var workspaceId = await connection.ExecuteScalarAsync<string>("SELECT id FROM workspace LIMIT 1");
                Assert.False(string.IsNullOrWhiteSpace(workspaceId));
            }

            var backupService = new SqliteBackupService(settingsService);
            var outputZipPath = Path.Combine(tempRoot, "workspace-backup.zip");

            await backupService.ExportWorkspaceBackupAsync(string.Empty, outputZipPath);

            Assert.True(File.Exists(outputZipPath));

            using var archive = ZipFile.OpenRead(outputZipPath);
            Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
            Assert.Contains(archive.Entries, entry => entry.FullName.EndsWith(".db", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(archive.Entries, entry => entry.FullName.Contains("vault/", StringComparison.OrdinalIgnoreCase));
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
