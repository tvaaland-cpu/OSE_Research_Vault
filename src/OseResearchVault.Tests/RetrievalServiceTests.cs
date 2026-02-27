using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class RetrievalServiceTests
{
    [Fact]
    public async Task RetrieveAsync_ReturnsExpectedTypesAndCitations()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true, Pooling = false }.ToString());
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var companyId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("O");

            await connection.ExecuteAsync("INSERT INTO company(id, workspace_id, name, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Name, @Now, @Now)", new { Id = companyId, WorkspaceId = workspaceId, Name = "Acme", Now = now });

            var noteId = Guid.NewGuid().ToString();
            await connection.ExecuteAsync("INSERT INTO note(id, workspace_id, company_id, title, content, created_at, updated_at) VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Content, @Now, @Now)", new { Id = noteId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "OEM note", Content = "tier-1 OEM demand rose in Q2.", Now = now });
            await connection.ExecuteAsync("INSERT INTO note_fts(id, title, body) VALUES (@Id, @Title, @Body)", new { Id = noteId, Title = "OEM note", Body = "tier-1 OEM demand rose in Q2." });

            var docId = Guid.NewGuid().ToString();
            await connection.ExecuteAsync("INSERT INTO document(id, workspace_id, company_id, title, created_at, updated_at) VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Now, @Now)", new { Id = docId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "OEM transcript", Now = now });
            await connection.ExecuteAsync("INSERT INTO document_text(id, document_id, chunk_index, content, created_at, updated_at) VALUES (@Id, @DocumentId, 0, @Content, @Now, @Now)", new { Id = Guid.NewGuid().ToString(), DocumentId = docId, Content = "Management discussed tier-1 OEM build schedules.", Now = now });
            await connection.ExecuteAsync("INSERT INTO document_text_fts(id, title, content) VALUES (@Id, @Title, @Content)", new { Id = docId, Title = "OEM transcript", Content = "Management discussed tier-1 OEM build schedules." });

            var snippetId = Guid.NewGuid().ToString();
            await connection.ExecuteAsync("INSERT INTO snippet(id, workspace_id, document_id, quote_text, context, created_at, updated_at) VALUES (@Id, @WorkspaceId, @DocumentId, @Quote, @Context, @Now, @Now)", new { Id = snippetId, WorkspaceId = workspaceId, DocumentId = docId, Quote = "tier-1 OEM build schedules", Context = "call notes p.2", Now = now });
            await connection.ExecuteAsync("INSERT INTO snippet_fts(id, text) VALUES (@Id, @Text)", new { Id = snippetId, Text = "tier-1 OEM build schedules call notes" });

            var artifactId = Guid.NewGuid().ToString();
            await connection.ExecuteAsync("INSERT INTO artifact(id, workspace_id, artifact_type, title, content, created_at, updated_at) VALUES (@Id, @WorkspaceId, 'memo', @Title, @Content, @Now, @Now)", new { Id = artifactId, WorkspaceId = workspaceId, Title = "OEM memo", Content = "Analyst memo on tier-1 OEM channels.", Now = now });
            await connection.ExecuteAsync("INSERT INTO artifact_fts(id, content) VALUES (@Id, @Content)", new { Id = artifactId, Content = "Analyst memo on tier-1 OEM channels." });

            var service = new SqliteRetrievalService(settingsService, NullLogger<SqliteRetrievalService>.Instance);
            var result = await service.RetrieveAsync(workspaceId, "tier-1 OEM", companyId, limitPerType: 5, maxTotalChars: 10_000);

            Assert.Equal("tier-1 OEM", result.Query);
            Assert.Contains(result.Items, i => i.ItemType == "note" && i.CitationLabel.StartsWith($"[NOTE:{noteId}|chunk:0]", StringComparison.Ordinal));
            Assert.Contains(result.Items, i => i.ItemType == "doc" && i.CitationLabel.StartsWith($"[DOC:{docId}|chunk:0]", StringComparison.Ordinal));
            Assert.Contains(result.Items, i => i.ItemType == "snippet" && i.CitationLabel == $"[SNIP:{snippetId}]");
            Assert.Contains(result.Items, i => i.ItemType == "artifact" && i.CitationLabel.StartsWith($"[ART:{artifactId}|chunk:0]", StringComparison.Ordinal));

            Assert.Equal("tier-1 OEM", result.Log.Query);
            Assert.Equal(companyId, result.Log.CompanyId);
            Assert.True(result.Log.NoteCount >= 1);
            Assert.True(result.Log.DocumentCount >= 1);
            Assert.True(result.Log.SnippetCount >= 1);
            Assert.True(result.Log.ArtifactCount >= 1);
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
    public async Task RetrieveAsync_RespectsMaxTotalCharsCap()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true, Pooling = false }.ToString());
            await connection.OpenAsync();

            var workspaceId = await connection.QuerySingleAsync<string>("SELECT id FROM workspace LIMIT 1");
            var noteId = Guid.NewGuid().ToString();
            var longText = string.Join(' ', Enumerable.Repeat("tier-1 OEM commentary", 300));
            var now = DateTime.UtcNow.ToString("O");

            await connection.ExecuteAsync("INSERT INTO note(id, workspace_id, title, content, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Title, @Content, @Now, @Now)", new { Id = noteId, WorkspaceId = workspaceId, Title = "Long OEM note", Content = longText, Now = now });
            await connection.ExecuteAsync("INSERT INTO note_fts(id, title, body) VALUES (@Id, @Title, @Body)", new { Id = noteId, Title = "Long OEM note", Body = longText });

            var service = new SqliteRetrievalService(settingsService, NullLogger<SqliteRetrievalService>.Instance);
            var result = await service.RetrieveAsync(workspaceId, "tier-1 OEM", companyId: null, limitPerType: 10, maxTotalChars: 600);

            var totalChars = result.Items.Sum(i => i.TextExcerpt.Length);
            Assert.True(totalChars <= 600);
            Assert.NotEmpty(result.Items);
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
