using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteAgentService(
    IAppSettingsService appSettingsService,
    ILLMProviderFactory llmProviderFactory) : IAgentService
{
    public async Task<IReadOnlyList<AgentTemplateRecord>> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AgentTemplateRecord>(new CommandDefinition(
            @"SELECT id,
                     name,
                     COALESCE(goal, '') AS Goal,
                     COALESCE(instructions, '') AS Instructions,
                     COALESCE(allowed_tools_json, '[]') AS AllowedToolsJson,
                     COALESCE(output_schema, '') AS OutputSchema,
                     COALESCE(evidence_policy, '') AS EvidencePolicy,
                     created_at AS CreatedAt
                FROM agent
            ORDER BY created_at DESC", cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<string> CreateAgentAsync(AgentTemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var agentId = Guid.NewGuid().ToString();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO agent (id, workspace_id, name, goal, instructions, allowed_tools_json, output_schema, evidence_policy, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @Name, @Goal, @Instructions, @AllowedToolsJson, @OutputSchema, @EvidencePolicy, @Now, @Now)",
            new
            {
                Id = agentId,
                WorkspaceId = workspaceId,
                Name = request.Name.Trim(),
                Goal = Clean(request.Goal),
                Instructions = Clean(request.Instructions),
                AllowedToolsJson = ValidateJsonList(request.AllowedToolsJson),
                OutputSchema = Clean(request.OutputSchema),
                EvidencePolicy = Clean(request.EvidencePolicy),
                Now = now
            }, cancellationToken: cancellationToken));

        return agentId;
    }

    public async Task UpdateAgentAsync(string agentId, AgentTemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE agent
                 SET name = @Name,
                     goal = @Goal,
                     instructions = @Instructions,
                     allowed_tools_json = @AllowedToolsJson,
                     output_schema = @OutputSchema,
                     evidence_policy = @EvidencePolicy,
                     updated_at = @Now
               WHERE id = @Id",
            new
            {
                Id = agentId,
                Name = request.Name.Trim(),
                Goal = Clean(request.Goal),
                Instructions = Clean(request.Instructions),
                AllowedToolsJson = ValidateJsonList(request.AllowedToolsJson),
                OutputSchema = Clean(request.OutputSchema),
                EvidencePolicy = Clean(request.EvidencePolicy),
                Now = DateTime.UtcNow.ToString("O")
            }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AgentRunRecord>> GetRunsAsync(string? agentId = null, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AgentRunRecord>(new CommandDefinition(
            @"SELECT ar.id,
                     ar.agent_id AS AgentId,
                     a.name AS AgentName,
                     ar.status,
                     ar.company_id AS CompanyId,
                     c.name AS CompanyName,
                     COALESCE(ar.query_text, '') AS Query,
                     COALESCE(ar.selected_document_ids_json, '[]') AS SelectedDocumentIdsJson,
                     COALESCE(ar.model_provider, '') AS ModelProvider,
                     COALESCE(ar.model_name, '') AS ModelName,
                     COALESCE(ar.model_parameters_json, '{}') AS ModelParametersJson,
                     COALESCE(ar.error, '') AS Error,
                     ar.started_at AS StartedAt,
                     ar.finished_at AS FinishedAt
                FROM agent_run ar
                INNER JOIN agent a ON a.id = ar.agent_id
                LEFT JOIN company c ON c.id = ar.company_id
               WHERE (@AgentId IS NULL OR ar.agent_id = @AgentId)
            ORDER BY ar.started_at DESC",
            new { AgentId = agentId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<string> CreateRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var runId = Guid.NewGuid().ToString();
        var artifactId = Guid.NewGuid().ToString();
        var generationSettings = settings.DefaultLlmSettings;

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var agent = await connection.QuerySingleAsync<AgentTemplateRecord>(new CommandDefinition(
            @"SELECT id,
                     name,
                     COALESCE(goal, '') AS Goal,
                     COALESCE(instructions, '') AS Instructions,
                     COALESCE(allowed_tools_json, '[]') AS AllowedToolsJson,
                     COALESCE(output_schema, '') AS OutputSchema,
                     COALESCE(evidence_policy, '') AS EvidencePolicy,
                     created_at AS CreatedAt
                FROM agent
               WHERE id = @Id",
            new { Id = request.AgentId }, cancellationToken: cancellationToken));

        var selectedChunks = await SelectRelevantChunksAsync(connection, request, generationSettings.TopDocumentChunks, cancellationToken);
        var prompt = BuildPrompt(agent, request.Query ?? string.Empty, selectedChunks);
        var contextDocsText = string.Join("\n\n", selectedChunks.Select(c => $"[{c.DocumentTitle}#{c.ChunkIndex}] {c.Content}"));

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO agent_run (id, agent_id, workspace_id, company_id, status, query_text, selected_document_ids_json, model_provider, model_name, model_parameters_json, started_at, finished_at)
              VALUES (@Id, @AgentId, @WorkspaceId, @CompanyId, 'running', @Query, @SelectedDocumentIdsJson, @ModelProvider, @ModelName, @ModelParametersJson, @Now, NULL)",
            new
            {
                Id = runId,
                request.AgentId,
                WorkspaceId = workspaceId,
                CompanyId = Clean(request.CompanyId),
                Query = Clean(request.Query),
                SelectedDocumentIdsJson = JsonSerializer.Serialize(request.SelectedDocumentIds.Distinct()),
                ModelProvider = generationSettings.Provider,
                ModelName = generationSettings.Model,
                ModelParametersJson = JsonSerializer.Serialize(new { generationSettings.Temperature, generationSettings.MaxTokens, generationSettings.TopDocumentChunks }),
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO tool_call (id, agent_run_id, name, arguments_json, output_json, status, created_at)
              VALUES (@Id, @RunId, 'local_search', @ArgumentsJson, @OutputJson, 'success', @Now)",
            new
            {
                Id = Guid.NewGuid().ToString(),
                RunId = runId,
                ArgumentsJson = JsonSerializer.Serialize(new { query = request.Query, selectedDocuments = request.SelectedDocumentIds }),
                OutputJson = JsonSerializer.Serialize(selectedChunks.Select(x => new { x.DocumentId, x.DocumentTextId, x.ChunkIndex, x.Score })),
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        var llmProvider = llmProviderFactory.GetProvider(generationSettings.Provider);
        string responseText;
        string runStatus;
        string? runError = null;
        try
        {
            responseText = await llmProvider.GenerateAsync(prompt, contextDocsText, generationSettings, cancellationToken);
            runStatus = "success";
        }
        catch (Exception ex)
        {
            responseText = $"Generation failed: {ex.Message}";
            runStatus = "failed";
            runError = ex.Message;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO artifact (id, workspace_id, agent_run_id, artifact_type, title, content, content_format, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @RunId, 'agent_output', @Title, @Content, 'text', @Now, @Now)",
            new { Id = artifactId, WorkspaceId = workspaceId, RunId = runId, Title = "Run Output", Content = responseText, Now = now },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var chunk in selectedChunks)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO evidence_link (id, workspace_id, from_entity_type, from_entity_id, to_entity_type, to_entity_id, relation, confidence, created_at)
                  VALUES (@Id, @WorkspaceId, 'agent_run', @RunId, 'document_text', @ChunkId, 'used_context', @Confidence, @Now)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkspaceId = workspaceId,
                    RunId = runId,
                    ChunkId = chunk.DocumentTextId,
                    Confidence = Math.Min(1d, chunk.Score),
                    Now = now
                }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE agent_run
                 SET status = @Status,
                     error = @Error,
                     finished_at = @Now
               WHERE id = @RunId",
            new { RunId = runId, Status = runStatus, Error = runError, Now = DateTime.UtcNow.ToString("O") }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return runId;
    }

    public async Task<IReadOnlyList<AgentArtifactRecord>> GetArtifactsAsync(string runId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AgentArtifactRecord>(new CommandDefinition(
            @"SELECT id,
                     agent_run_id AS RunId,
                     COALESCE(title, '(untitled artifact)') AS Title,
                     COALESCE(content, '') AS Content,
                     created_at AS CreatedAt
                FROM artifact
               WHERE agent_run_id = @RunId
            ORDER BY created_at",
            new { RunId = runId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task UpdateArtifactContentAsync(string artifactId, string content, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE artifact
                 SET content = @Content,
                     updated_at = @Now
               WHERE id = @Id",
            new { Id = artifactId, Content = content, Now = DateTime.UtcNow.ToString("O") }, cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<DocumentChunkScore>> SelectRelevantChunksAsync(SqliteConnection connection, AgentRunRequest request, int takeCount, CancellationToken cancellationToken)
    {
        var ids = request.SelectedDocumentIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var chunks = (await connection.QueryAsync<DocumentChunkScore>(new CommandDefinition(
            @"SELECT dt.id AS DocumentTextId,
                     dt.document_id AS DocumentId,
                     dt.chunk_index AS ChunkIndex,
                     dt.content AS Content,
                     d.title AS DocumentTitle,
                     0.0 AS Score
                FROM document_text dt
                INNER JOIN document d ON d.id = dt.document_id
               WHERE dt.document_id IN @Ids", new { Ids = ids }, cancellationToken: cancellationToken))).ToList();

        var queryTokens = Tokenize(request.Query).ToArray();
        if (queryTokens.Length == 0)
        {
            return chunks.OrderBy(x => x.ChunkIndex).Take(Math.Max(1, takeCount)).ToList();
        }

        var tokenDocFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in queryTokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            tokenDocFreq[token] = chunks.Count(c => c.Content.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var chunk in chunks)
        {
            var score = 0d;
            foreach (var token in queryTokens)
            {
                var tf = CountOccurrences(chunk.Content, token);
                if (tf == 0)
                {
                    continue;
                }

                var idf = Math.Log((chunks.Count + 1d) / (1d + tokenDocFreq[token])) + 1d;
                score += tf * idf;
            }

            chunk.Score = score;
        }

        return chunks
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.ChunkIndex)
            .Take(Math.Max(1, takeCount))
            .ToList();
    }

    private static int CountOccurrences(string content, string token)
    {
        var count = 0;
        var index = 0;
        while (index < content.Length)
        {
            var found = content.IndexOf(token, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                break;
            }

            count++;
            index = found + token.Length;
        }

        return count;
    }

    private static IEnumerable<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var token in text.Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '?', '!'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length >= 2)
            {
                yield return token;
            }
        }
    }

    private static string BuildPrompt(AgentTemplateRecord agent, string query, IReadOnlyList<DocumentChunkScore> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Agent: {agent.Name}");
        if (!string.IsNullOrWhiteSpace(agent.Goal))
        {
            builder.AppendLine($"Goal: {agent.Goal}");
        }

        if (!string.IsNullOrWhiteSpace(agent.Instructions))
        {
            builder.AppendLine($"Instructions: {agent.Instructions}");
        }

        builder.AppendLine();
        builder.AppendLine($"User query: {query}");
        builder.AppendLine($"Relevant chunks provided: {chunks.Count}");
        return builder.ToString();
    }

    private static string ValidateJsonList(string? json)
    {
        var cleaned = string.IsNullOrWhiteSpace(json) ? "[]" : json.Trim();

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Allowed tools must be a JSON array.");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Allowed tools must be valid JSON.", ex);
        }

        return cleaned;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString());
    }

    private static async Task<string> EnsureWorkspaceAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync(cancellationToken);

        var workspaceId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM workspace ORDER BY created_at LIMIT 1", cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaceId;
        }

        workspaceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO workspace (id, name, description, created_at, updated_at)
              VALUES (@Id, @Name, @Description, @Now, @Now)",
            new { Id = workspaceId, Name = "Default Workspace", Description = "Auto-created workspace", Now = now }, cancellationToken: cancellationToken));

        return workspaceId;
    }

    private sealed class DocumentChunkScore
    {
        public string DocumentTextId { get; init; } = string.Empty;
        public string DocumentId { get; init; } = string.Empty;
        public int ChunkIndex { get; init; }
        public string Content { get; init; } = string.Empty;
        public string DocumentTitle { get; init; } = string.Empty;
        public double Score { get; set; }
    }
}
