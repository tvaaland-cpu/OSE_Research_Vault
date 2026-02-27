using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class DataQualityServiceTests
{
    [Fact]
    public async Task ReportAndFixupActionsWork()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ids = await SeedDataAsync(settingsService);
            var service = new SqliteDataQualityService(settingsService);

            var report = await service.GetReportAsync();
            Assert.Single(report.Duplicates);
            Assert.Single(report.UnlinkedDocuments);
            Assert.Single(report.UnlinkedNotes);
            Assert.Single(report.EvidenceGaps);
            Assert.Single(report.SnippetIssues);
            Assert.Equal(2, report.EnrichmentSuggestions.Count);

            var documentSuggestion = report.EnrichmentSuggestions.Single(s => s.ItemType == "document");
            Assert.Equal(ids.UnlinkedDocumentId, documentSuggestion.ItemId);
            Assert.Equal(ids.CompanyId, documentSuggestion.CompanyId);

            var noteSuggestion = report.EnrichmentSuggestions.Single(s => s.ItemType == "note");
            Assert.Equal(ids.UnlinkedNoteId, noteSuggestion.ItemId);

            await service.ApplyEnrichmentSuggestionAsync(documentSuggestion.ItemType, documentSuggestion.ItemId, documentSuggestion.CompanyId);
            await service.ApplyEnrichmentSuggestionAsync(noteSuggestion.ItemType, noteSuggestion.ItemId, noteSuggestion.CompanyId);
            await service.ArchiveDuplicateDocumentsAsync(ids.DuplicateHash, ids.KeepDocumentId);

            var afterFix = await service.GetReportAsync();
            Assert.Empty(afterFix.Duplicates);
            Assert.Empty(afterFix.UnlinkedDocuments);
            Assert.Empty(afterFix.UnlinkedNotes);
        }
        finally
        {
            Cleanup(tempRoot);
        }
    }

    private static async Task<(string CompanyId, string UnlinkedDocumentId, string UnlinkedNoteId, string KeepDocumentId, string DuplicateHash)> SeedDataAsync(IAppSettingsService settingsService)
    {
        var settings = await settingsService.GetSettingsAsync();
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true, Pooling = false }.ToString());
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");
        var workspaceId = Guid.NewGuid().ToString();
        var companyId = Guid.NewGuid().ToString();
        var duplicateHash = "hash-dup-1";
        var keepId = Guid.NewGuid().ToString();
        var archiveId = Guid.NewGuid().ToString();
        var unlinkedDocId = Guid.NewGuid().ToString();
        var unlinkedNoteId = Guid.NewGuid().ToString();
        var snippetIssueId = Guid.NewGuid().ToString();

        await connection.ExecuteAsync("INSERT INTO workspace (id, name, created_at, updated_at) VALUES (@Id, @Name, @Now, @Now)", new { Id = workspaceId, Name = "Default", Now = now });
        await connection.ExecuteAsync("INSERT INTO company (id, workspace_id, name, ticker, isin, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Name, @Ticker, @Isin, @Now, @Now)", new { Id = companyId, WorkspaceId = workspaceId, Name = "Acme", Ticker = "NAPA.OL", Isin = "NO0010816924", Now = now });

        await connection.ExecuteAsync(@"INSERT INTO document (id, workspace_id, company_id, title, content_hash, imported_at, file_path, created_at, updated_at)
                                       VALUES (@Id, @WorkspaceId, @CompanyId, @Title, @Hash, @Now, @Path, @Now, @Now)",
            new[]
            {
                new { Id = keepId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "Dup Keep", Hash = duplicateHash, Now = now, Path = "/tmp/a.txt" },
                new { Id = archiveId, WorkspaceId = workspaceId, CompanyId = companyId, Title = "Dup Archive", Hash = duplicateHash, Now = now, Path = "/tmp/b.txt" },
                new { Id = unlinkedDocId, WorkspaceId = workspaceId, CompanyId = (string?)null, Title = "Unlinked", Hash = "other-hash", Now = now, Path = "/tmp/c.txt" }
            });

        await connection.ExecuteAsync("INSERT INTO document_text (id, document_id, content, created_at) VALUES (@Id, @DocumentId, @Content, @Now)",
            new { Id = Guid.NewGuid().ToString(), DocumentId = unlinkedDocId, Content = "Meeting notes mention NAPA.OL and its guidance.", Now = now });

        await connection.ExecuteAsync("INSERT INTO note (id, workspace_id, title, content, note_type, created_at, updated_at) VALUES (@Id, @WorkspaceId, @Title, @Content, 'manual', @Now, @Now)",
            new { Id = unlinkedNoteId, WorkspaceId = workspaceId, Title = "Unlinked Note", Content = "NAPA.OL still looks cheap", Now = now });

        await connection.ExecuteAsync("INSERT INTO artifact (id, workspace_id, artifact_type, title, created_at, updated_at) VALUES (@Id, @WorkspaceId, 'summary', @Title, @Now, @Now)",
            new { Id = Guid.NewGuid().ToString(), WorkspaceId = workspaceId, Title = "Gap Artifact", Now = now });

        await connection.ExecuteAsync("INSERT INTO snippet (id, workspace_id, context, quote_text, created_at, updated_at) VALUES (@Id, @WorkspaceId, '', 'bad snippet', @Now, @Now)",
            new { Id = snippetIssueId, WorkspaceId = workspaceId, Now = now });

        return (companyId, unlinkedDocId, unlinkedNoteId, keepId, duplicateHash);
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
