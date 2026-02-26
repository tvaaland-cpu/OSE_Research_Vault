using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class RunRerunDiffTests
{
    [Fact]
    public async Task CreateRunAsync_PersistsParentRunId()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();
            var providerFactory = new LlmProviderFactory([new LocalEchoLlmProvider()]);
            var agentService = new SqliteAgentService(settingsService, providerFactory);

            var agentId = await agentService.CreateAgentAsync(new AgentTemplateUpsertRequest
            {
                Name = "Rerunnable",
                Goal = "Answer",
                Instructions = "Answer",
                AllowedToolsJson = "[]",
                OutputSchema = "text",
                EvidencePolicy = "strict"
            });

            var parentRunId = await agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = agentId,
                Query = "Parent",
                SelectedDocumentIds = []
            });

            var childRunId = await agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = agentId,
                ParentRunId = parentRunId,
                Query = "Child",
                SelectedDocumentIds = []
            });

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true }.ToString());
            await connection.OpenAsync();

            var storedParentRunId = await connection.QuerySingleAsync<string?>("SELECT parent_run_id FROM agent_run WHERE id = @Id", new { Id = childRunId });
            Assert.Equal(parentRunId, storedParentRunId);
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
    public void Compare_ComputesLineDiffAndEvidenceCounts()
    {
        var service = new RunDiffService();
        var originalLinks = new List<EvidenceLink>
        {
            new() { Id = "1", DocumentId = "doc-a", SnippetId = "snip-1" },
            new() { Id = "2", DocumentId = "doc-a", SnippetId = "snip-2" },
            new() { Id = "3", DocumentId = "doc-b", SnippetId = null }
        };
        var rerunLinks = new List<EvidenceLink>
        {
            new() { Id = "4", DocumentId = "doc-b", SnippetId = "snip-2" }
        };

        var result = service.Compare("line1\nline2", "line1\nline3", originalLinks, rerunLinks);

        Assert.Contains("  line1", result.TextDiff);
        Assert.Contains("- line2", result.TextDiff);
        Assert.Contains("+ line3", result.TextDiff);
        Assert.Equal(3, result.OriginalEvidence.LinkCount);
        Assert.Equal(2, result.OriginalEvidence.UniqueDocumentCount);
        Assert.Equal(2, result.OriginalEvidence.SnippetCount);
        Assert.Equal(1, result.RerunEvidence.LinkCount);
        Assert.Equal(1, result.RerunEvidence.UniqueDocumentCount);
        Assert.Equal(1, result.RerunEvidence.SnippetCount);
    }
}
