using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;
using System.Text.RegularExpressions;

namespace OseResearchVault.Tests;

public sealed class AgentRunFlowTests
{
    [Fact]
    public async Task CanCreateTemplateRunAndPersistAuditableArtifacts()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var docService = new SqliteDocumentImportService(settingsService, ftsSyncService, NullLogger<SqliteDocumentImportService>.Instance);
            var companyService = new SqliteCompanyService(settingsService);
            var providerFactory = new LlmProviderFactory([new LocalEchoLlmProvider()]);
            var agentService = new SqliteAgentService(settingsService, providerFactory);

            var companyId = await companyService.CreateCompanyAsync(new CompanyUpsertRequest { Name = "ACME" }, []);

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);
            var txtPath = Path.Combine(inputDirectory, "memo.txt");
            await File.WriteAllTextAsync(txtPath, "internal memo changed guidance");
            var importResults = await docService.ImportFilesAsync([txtPath]);
            Assert.True(importResults[0].Succeeded);
            var doc = (await docService.GetDocumentsAsync()).Single();

            var agentId = await agentService.CreateAgentAsync(new AgentTemplateUpsertRequest
            {
                Name = "MVP Analyst",
                Goal = "Summarize",
                Instructions = "Use provided docs",
                AllowedToolsJson = "[\"local_docs\"]",
                OutputSchema = "text",
                EvidencePolicy = "strict"
            });

            var runId = await agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = agentId,
                CompanyId = companyId,
                Query = "What changed?",
                SelectedDocumentIds = [doc.Id]
            });

            var artifacts = await agentService.GetArtifactsAsync(runId);
            var artifact = Assert.Single(artifacts);
            Assert.Contains("LocalEcho", artifact.Content);

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = settings.DatabaseFilePath,
                ForeignKeys = true
            }.ToString());
            await connection.OpenAsync();

            var run = await connection.QuerySingleAsync<(string Status, string ModelProvider, string ModelName, string ModelParametersJson)>(
                "SELECT status, model_provider as ModelProvider, model_name as ModelName, model_parameters_json as ModelParametersJson FROM agent_run WHERE id = @Id",
                new { Id = runId });
            Assert.Equal("success", run.Status);
            Assert.Equal("local", run.ModelProvider);
            Assert.Equal("local-echo", run.ModelName);
            Assert.Contains("TopDocumentChunks", run.ModelParametersJson);

            var toolCalls = (await connection.QueryAsync<(string Name, string Status)>("SELECT name, status FROM tool_call WHERE agent_run_id = @Id", new { Id = runId })).ToList();
            Assert.Contains(toolCalls, call => call.Name == "local_search" && call.Status == "success");
            Assert.Contains(toolCalls, call => call.Name == "prompt_build" && call.Status == "success");

            var evidenceCount = await connection.QuerySingleAsync<int>("SELECT COUNT(1) FROM evidence_link WHERE from_entity_type = 'agent_run' AND from_entity_id = @Id", new { Id = runId });
            Assert.True(evidenceCount > 0);
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
    public async Task CreateRunAsync_ParsesSnippetAndDocumentCitationsIntoEvidenceLinks()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var ftsSyncService = new SqliteFtsSyncService(settingsService);
            var docService = new SqliteDocumentImportService(settingsService, ftsSyncService, NullLogger<SqliteDocumentImportService>.Instance);
            var providerFactory = new LlmProviderFactory([new CitationLlmProvider()]);
            var agentService = new SqliteAgentService(settingsService, providerFactory);

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);
            var txtPath = Path.Combine(inputDirectory, "memo.txt");
            await File.WriteAllTextAsync(txtPath, "operating margin expanded in q4");
            await docService.ImportFilesAsync([txtPath]);
            var doc = (await docService.GetDocumentsAsync()).Single();

            var agentId = await agentService.CreateAgentAsync(new AgentTemplateUpsertRequest
            {
                Name = "AskMyVault",
                Instructions = "cite sources"
            });

            var runId = await agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = agentId,
                Query = "What changed?",
                SelectedDocumentIds = [doc.Id]
            });

            var settings = await settingsService.GetSettingsAsync();
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = settings.DatabaseFilePath, ForeignKeys = true }.ToString());
            await connection.OpenAsync();

            var artifactId = await connection.QuerySingleAsync<string>("SELECT id FROM artifact WHERE agent_run_id = @RunId", new { RunId = runId });
            var linkTypes = (await connection.QueryAsync<string>("SELECT to_entity_type FROM evidence_link WHERE from_entity_type = 'artifact' AND from_entity_id = @ArtifactId", new { ArtifactId = artifactId })).ToList();
            Assert.Contains("document", linkTypes);
            Assert.Contains("snippet", linkTypes);

            var toolCallNames = (await connection.QueryAsync<string>("SELECT name FROM tool_call WHERE agent_run_id = @RunId", new { RunId = runId })).ToList();
            Assert.Contains("local_search", toolCallNames);
            Assert.Contains("prompt_build", toolCallNames);
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
            VaultStorageDirectory = Path.Combine(rootDirectory, "vault"),
            DefaultLlmSettings = new LlmGenerationSettings
            {
                Provider = "local",
                Model = "local-echo",
                TopDocumentChunks = 3
            }
        };

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_settings.DatabaseDirectory);
            Directory.CreateDirectory(_settings.VaultStorageDirectory);
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CitationLlmProvider : ILLMProvider
    {
        public string ProviderName => "local";

        public Task<string> GenerateAsync(string prompt, string contextDocsText, LlmGenerationSettings settings, CancellationToken cancellationToken = default)
        {
            var docMatch = Regex.Match(prompt, @"DOC:(?<id>[^|\]]+)\|chunk:(?<chunk>\d+)", RegexOptions.IgnoreCase);
            var docId = docMatch.Success ? docMatch.Groups["id"].Value : "missing-doc";
            var chunk = docMatch.Success ? docMatch.Groups["chunk"].Value : "0";
            return Task.FromResult($"Answer with citations [SNIP:test-snippet] and [DOC:{docId}|chunk:{chunk}]");
        }
    }
}
