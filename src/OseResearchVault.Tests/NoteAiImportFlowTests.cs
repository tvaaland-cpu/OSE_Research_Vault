using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class NoteAiImportFlowTests
{
    [Fact]
    public async Task ImportAiOutputAsync_CreatesAiSummaryNoteAndArtifact()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var noteService = new SqliteNoteService(settingsService, ftsSyncService);

            await noteService.ImportAiOutputAsync(new AiImportRequest
            {
                Model = "gpt-test",
                Prompt = "Summarize the quarter.",
                Response = "Revenue grew 14% year-over-year.",
                Sources = "Q4 Report\nEarnings Call"
            });

            var notes = await noteService.GetNotesAsync();
            var aiNote = Assert.Single(notes);
            Assert.Equal("ai_summary", aiNote.NoteType);
            Assert.Contains("## Prompt", aiNote.Content, StringComparison.Ordinal);
            Assert.Contains("## Response", aiNote.Content, StringComparison.Ordinal);
            Assert.Contains("## Sources", aiNote.Content, StringComparison.Ordinal);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var artifact = await connection.QuerySingleAsync<(string artifact_type, string content_format, string content, string metadata_json)>(
                "SELECT artifact_type, content_format, content, metadata_json FROM artifact LIMIT 1");

            Assert.Equal("summary", artifact.artifact_type);
            Assert.Equal("markdown", artifact.content_format);
            Assert.Equal("Revenue grew 14% year-over-year.", artifact.content);
            Assert.Contains("gpt-test", artifact.metadata_json, StringComparison.Ordinal);
            Assert.Contains("Summarize the quarter.", artifact.metadata_json, StringComparison.Ordinal);

            var noteFtsRows = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM note_fts");
            var artifactFtsRows = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM artifact_fts");
            Assert.Equal(1, noteFtsRows);
            Assert.Equal(1, artifactFtsRows);
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
