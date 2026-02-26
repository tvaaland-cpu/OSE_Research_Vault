using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class SnapshotServiceTests
{
    [Fact]
    public async Task SaveUrlSnapshotAsync_Html_CreatesSourceAndDocument()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new NoOpFtsSyncService();
            var snapshotService = new SnapshotService(settingsService, new FakeConnectorHttpClient("<html><head><title>Example</title></head><body><h1>Hello Vault</h1></body></html>"), ftsSyncService);

            var workspaceId = await EnsureWorkspaceAsync(settingsService);
            var result = await snapshotService.SaveUrlSnapshotAsync("https://example.com", workspaceId, null, "html");

            await using var connection = OpenConnection((await settingsService.GetSettingsAsync()).DatabaseFilePath);
            await connection.OpenAsync();

            var sourceCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM source WHERE id = @Id", new { Id = result.SourceId });
            var documentCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM document WHERE id = @Id AND source_id = @SourceId", new { Id = result.DocumentId, SourceId = result.SourceId });
            var text = await connection.QuerySingleOrDefaultAsync<string>("SELECT content FROM document_text WHERE document_id = @Id", new { Id = result.DocumentId });

            Assert.Equal(1, sourceCount);
            Assert.Equal(1, documentCount);
            Assert.Contains("Hello Vault", text);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task<string> EnsureWorkspaceAsync(IAppSettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync();

        var workspaceId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync("INSERT INTO workspace (id, name, created_at) VALUES (@Id, 'Test', @Now)", new { Id = workspaceId, Now = DateTime.UtcNow.ToString("O") });
        return workspaceId;
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

    private sealed class FakeConnectorHttpClient(string html) : IConnectorHttpClient
    {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(html);
        public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    }

    private sealed class NoOpFtsSyncService : IFtsSyncService
    {
        public Task UpsertNoteAsync(string id, string title, string content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertSnippetAsync(string id, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteSnippetAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertArtifactAsync(string id, string? content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteArtifactAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertDocumentTextAsync(string id, string title, string content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteDocumentTextAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
