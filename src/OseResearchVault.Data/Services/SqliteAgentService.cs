using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteAgentService(
    IAppSettingsService appSettingsService,
    ILLMProviderFactory llmProviderFactory) : IAgentService
{
    private static readonly Regex SnippetCitationRegex = new(@"\[SNIP:(?<id>[^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DocumentCitationRegex = new(@"\[DOC:(?<documentId>[^\]|]+)\|chunk:(?<chunkIndex>\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        var selectedChunks = await RetrieveRelevantChunksAsync(connection, request, generationSettings.TopDocumentChunks, cancellationToken);
        var promptBuildResult = BuildPrompt(agent, request.Query ?? string.Empty, selectedChunks);
        var prompt = promptBuildResult.Prompt;
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
                OutputJson = JsonSerializer.Serialize(new
                {
                    count = selectedChunks.Count,
                    documentIds = selectedChunks.Select(x => x.DocumentId).Distinct().ToArray(),
                    chunkIds = selectedChunks.Select(x => x.DocumentTextId).ToArray(),
                    chunks = selectedChunks.Select(x => new { x.DocumentId, x.DocumentTextId, x.ChunkIndex, x.Score })
                }),
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO tool_call (id, agent_run_id, name, arguments_json, output_json, status, created_at)
              VALUES (@Id, @RunId, 'prompt_build', @ArgumentsJson, @OutputJson, 'success', @Now)",
            new
            {
                Id = Guid.NewGuid().ToString(),
                RunId = runId,
                ArgumentsJson = JsonSerializer.Serialize(new { query = request.Query, selectedChunkCount = selectedChunks.Count }),
                OutputJson = JsonSerializer.Serialize(new { promptLength = prompt.Length, citationLabels = promptBuildResult.CitationLabels }),
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
              VALUES (@Id, @WorkspaceId, @RunId, 'summary', @Title, @Content, 'markdown', @Now, @Now)",
            new { Id = artifactId, WorkspaceId = workspaceId, RunId = runId, Title = "Ask My Vault Answer", Content = responseText, Now = now },
            transaction,
            cancellationToken: cancellationToken));

        var citationCount = await CreateCitationEvidenceLinksAsync(
            connection,
            transaction,
            workspaceId,
            artifactId,
            responseText,
            selectedChunks,
            cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO tool_call (id, agent_run_id, name, arguments_json, output_json, status, created_at)
              VALUES (@Id, @RunId, 'citation_parse', @ArgumentsJson, @OutputJson, 'success', @Now)",
            new
            {
                Id = Guid.NewGuid().ToString(),
                RunId = runId,
                ArgumentsJson = JsonSerializer.Serialize(new { artifactId }),
                OutputJson = JsonSerializer.Serialize(new { citationCount }),
                Now = now
            },
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

    public async Task<IReadOnlyList<AgentToolCallRecord>> GetToolCallsAsync(string runId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AgentToolCallRecord>(new CommandDefinition(
            @"SELECT id,
                     agent_run_id AS RunId,
                     name,
                     COALESCE(arguments_json, '{}') AS ArgumentsJson,
                     COALESCE(output_json, '{}') AS OutputJson,
                     status,
                     created_at AS CreatedAt
                FROM tool_call
               WHERE agent_run_id = @RunId
            ORDER BY created_at",
            new { RunId = runId }, cancellationToken: cancellationToken));

        return rows.ToList();
    public async Task<AskMyVaultResult> ExecuteAskMyVaultAsync(AskMyVaultRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var generationSettings = settings.DefaultLlmSettings;

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var askAgentId = await EnsureAskMyVaultAgentAsync(connection, workspaceId, now, cancellationToken);
        var selectedChunks = await SelectRelevantChunksAsync(connection, new AgentRunRequest { AgentId = askAgentId, Query = request.Query, SelectedDocumentIds = request.SelectedDocumentIds }, generationSettings.TopDocumentChunks, cancellationToken);
        var prompt = BuildAskMyVaultPrompt(request.Query, selectedChunks);
        var contextDocsText = string.Join("\n\n", selectedChunks.Select(c => $"[DOC:{c.DocumentId}|chunk:{c.ChunkIndex}] {c.Content}"));
        var promptLabels = selectedChunks.Select(c => $"DOC:{c.DocumentId}|chunk:{c.ChunkIndex}").Distinct().ToArray();

        var runId = Guid.NewGuid().ToString();
        var artifactId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO agent_run (id, agent_id, workspace_id, company_id, status, query_text, selected_document_ids_json, model_provider, model_name, model_parameters_json, started_at, finished_at)
              VALUES (@Id, @AgentId, @WorkspaceId, @CompanyId, 'running', @Query, @SelectedDocumentIdsJson, @ModelProvider, @ModelName, @ModelParametersJson, @Now, NULL)",
            new
            {
                Id = runId,
                AgentId = askAgentId,
                WorkspaceId = workspaceId,
                CompanyId = Clean(request.CompanyId),
                Query = Clean(request.Query),
                SelectedDocumentIdsJson = JsonSerializer.Serialize(request.SelectedDocumentIds.Distinct()),
                ModelProvider = generationSettings.Provider,
                ModelName = generationSettings.Model,
                ModelParametersJson = JsonSerializer.Serialize(new { generationSettings.Temperature, generationSettings.MaxTokens, generationSettings.TopDocumentChunks }),
                Now = now
            }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO tool_call (id, agent_run_id, name, arguments_json, output_json, status, created_at)
              VALUES (@Id, @RunId, 'local_search', @ArgumentsJson, @OutputJson, 'success', @Now)",
            new
            {
                Id = Guid.NewGuid().ToString(),
                RunId = runId,
                ArgumentsJson = JsonSerializer.Serialize(new { query = request.Query, companyId = request.CompanyId, selectedDocuments = request.SelectedDocumentIds }),
                OutputJson = JsonSerializer.Serialize(new { resultCount = selectedChunks.Count, documentIds = selectedChunks.Select(x => x.DocumentId).Distinct(), snippetIds = Array.Empty<string>(), chunkIds = selectedChunks.Select(x => x.DocumentTextId) }),
                Now = now
            }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO tool_call (id, agent_run_id, name, arguments_json, output_json, status, created_at)
              VALUES (@Id, @RunId, 'prompt_build', @ArgumentsJson, @OutputJson, 'success', @Now)",
            new
            {
                Id = Guid.NewGuid().ToString(),
                RunId = runId,
                ArgumentsJson = JsonSerializer.Serialize(new { query = request.Query, contextChunkCount = selectedChunks.Count }),
                OutputJson = JsonSerializer.Serialize(new { promptLength = prompt.Length, citationLabels = promptLabels }),
                Now = now
            }, transaction, cancellationToken: cancellationToken));

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
              VALUES (@Id, @WorkspaceId, @RunId, 'summary', @Title, @Content, 'markdown', @Now, @Now)",
            new { Id = artifactId, WorkspaceId = workspaceId, RunId = runId, Title = "Ask My Vault Summary", Content = responseText, Now = now }, transaction, cancellationToken: cancellationToken));

        var citationCount = await CreateCitationEvidenceLinksAsync(connection, transaction, workspaceId, artifactId, responseText, selectedChunks, now, cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE agent_run
                 SET status = @Status,
                     error = @Error,
                     finished_at = @Now
               WHERE id = @RunId",
            new { RunId = runId, Status = runStatus, Error = runError, Now = DateTime.UtcNow.ToString("O") }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return new AskMyVaultResult { RunId = runId, CitationsDetected = citationCount > 0 };
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

    public async Task<IReadOnlyList<AgentToolCallRecord>> GetToolCallsAsync(string runId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AgentToolCallRecord>(new CommandDefinition(
            @"SELECT id,
                     name,
                     COALESCE(arguments_json, '{}') AS ArgumentsJson,
                     COALESCE(output_json, '{}') AS OutputJson,
                     status,
                     created_at AS CreatedAt
                FROM tool_call
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

    private static async Task<IReadOnlyList<DocumentChunkScore>> RetrieveRelevantChunksAsync(SqliteConnection connection, AgentRunRequest request, int takeCount, CancellationToken cancellationToken)
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
                     COALESCE(d.imported_at, d.created_at) AS DocumentOccurredAt,
                     0.0 AS Score
                FROM document_text dt
                INNER JOIN document d ON d.id = dt.document_id
               WHERE dt.document_id IN @Ids", new { Ids = ids }, cancellationToken: cancellationToken))).ToList();

        var expansionTokens = await GetCompanyExpansionTokensAsync(connection, request.CompanyId, cancellationToken);
        var queryTokens = Tokenize(request.Query)
            .Concat(expansionTokens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

            var titleMatches = queryTokens.Sum(token => CountOccurrences(chunk.DocumentTitle, token));
            if (titleMatches > 0)
            {
                score += titleMatches * 2.25d;
            }

            score += CalculateRecencyBoost(chunk.DocumentId, chunks);

            chunk.Score = score;
        }

        var ranked = chunks
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.ChunkIndex)
            .ToList();

        return ApplyDocumentDedupe(ranked, Math.Max(1, takeCount))
            .Take(Math.Max(1, takeCount))
            .ToList();
    }

    private static IReadOnlyList<DocumentChunkScore> ApplyDocumentDedupe(IReadOnlyList<DocumentChunkScore> ranked, int takeCount)
    {
        var deduped = new List<DocumentChunkScore>(takeCount);
        var skipped = new List<DocumentChunkScore>();
        var perDocumentSignatures = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in ranked)
        {
            if (!perDocumentSignatures.TryGetValue(chunk.DocumentId, out var signatures))
            {
                signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                perDocumentSignatures[chunk.DocumentId] = signatures;
            }

            var signature = CreateDedupeSignature(chunk.Content);
            if (signatures.Add(signature))
            {
                deduped.Add(chunk);
                if (deduped.Count >= takeCount)
                {
                    return deduped;
                }

                continue;
            }

            skipped.Add(chunk);
        }

        foreach (var skippedChunk in skipped)
        {
            deduped.Add(skippedChunk);
            if (deduped.Count >= takeCount)
            {
                break;
            }
        }

        return deduped;
    }

    private static double CalculateRecencyBoost(string documentId, IReadOnlyCollection<DocumentChunkScore> chunks)
    {
        if (chunks.Count <= 1)
        {
            return 0d;
        }

        var documentDates = chunks
            .Select(x => new { x.DocumentId, Timestamp = ParseTimestamp(x.DocumentOccurredAt) })
            .GroupBy(x => x.DocumentId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { DocumentId = g.Key, Timestamp = g.Max(x => x.Timestamp) })
            .OrderByDescending(x => x.Timestamp)
            .ThenBy(x => x.DocumentId, StringComparer.Ordinal)
            .ToList();

        var rank = documentDates.FindIndex(x => string.Equals(x.DocumentId, documentId, StringComparison.OrdinalIgnoreCase));
        if (rank < 0)
        {
            return 0d;
        }

        var divisor = Math.Max(1d, documentDates.Count - 1d);
        return ((documentDates.Count - 1d - rank) / divisor) * 0.35d;
    }

    private static DateTime ParseTimestamp(string? value)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : DateTime.MinValue;
    }

    private static async Task<IReadOnlyList<string>> GetCompanyExpansionTokensAsync(SqliteConnection connection, string? companyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            return [];
        }

        var row = await connection.QuerySingleOrDefaultAsync<(string? Ticker, string? Isin)>(new CommandDefinition(
            "SELECT ticker AS Ticker, isin AS Isin FROM company WHERE id = @Id",
            new { Id = companyId },
            cancellationToken: cancellationToken));

        return Tokenize($"{row.Ticker} {row.Isin}").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string CreateDedupeSignature(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var chars = content
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray();

        var normalized = string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return normalized.Length <= 240 ? normalized : normalized[..240];
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

    private static PromptBuildResult BuildPrompt(AgentTemplateRecord agent, string query, IReadOnlyList<DocumentChunkScore> chunks)
    {
        var builder = new StringBuilder();
        var citationLabels = new List<string>();
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
        builder.AppendLine("Use citations like [SNIP:<id>] or [DOC:<document_id>|chunk:<i>] where possible.");
        builder.AppendLine($"Relevant chunks provided: {chunks.Count}");

        foreach (var chunk in chunks)
        {
            var label = $"DOC:{chunk.DocumentId}|chunk:{chunk.ChunkIndex}";
            citationLabels.Add(label);
            builder.AppendLine($"- [{label}] {chunk.DocumentTitle}");
        }

        return new PromptBuildResult(builder.ToString(), citationLabels);
    }

    private static async Task<int> CreateCitationEvidenceLinksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string workspaceId,
        string artifactId,
        string responseText,
        IReadOnlyList<DocumentChunkScore> selectedChunks,
        CancellationToken cancellationToken)
    {
        var snippetIds = SnippetCitationRegex.Matches(responseText)
            .Select(static m => m.Groups["id"].Value.Trim())
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var documentCitations = DocumentCitationRegex.Matches(responseText)
            .Select(static m => new
            {
                DocumentId = m.Groups["documentId"].Value.Trim(),
                ChunkIndex = int.TryParse(m.Groups["chunkIndex"].Value, out var chunkIndex) ? chunkIndex : -1
            })
            .Where(static x => !string.IsNullOrWhiteSpace(x.DocumentId) && x.ChunkIndex >= 0)
            .Distinct()
            .ToList();

        foreach (var snippetId in snippetIds)
        {
            var relation = JsonSerializer.Serialize(new { kind = "snippet" });
            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO evidence_link (id, workspace_id, from_entity_type, from_entity_id, to_entity_type, to_entity_id, relation, confidence, created_at)
                  VALUES (@Id, @WorkspaceId, 'artifact', @ArtifactId, 'snippet', @SnippetId, @Relation, 1.0, @Now)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkspaceId = workspaceId,
                    ArtifactId = artifactId,
                    SnippetId = snippetId,
                    Relation = relation,
                    Now = DateTime.UtcNow.ToString("O")
                }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var citation in documentCitations)
        {
            var matchedChunk = selectedChunks.FirstOrDefault(x => string.Equals(x.DocumentId, citation.DocumentId, StringComparison.OrdinalIgnoreCase) && x.ChunkIndex == citation.ChunkIndex);
            var relation = JsonSerializer.Serialize(new { kind = "document_locator", locator = $"chunk:{citation.ChunkIndex}", quote = matchedChunk?.Content });
            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO evidence_link (id, workspace_id, from_entity_type, from_entity_id, to_entity_type, to_entity_id, relation, confidence, created_at)
                  VALUES (@Id, @WorkspaceId, 'artifact', @ArtifactId, 'document', @DocumentId, @Relation, 1.0, @Now)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkspaceId = workspaceId,
                    ArtifactId = artifactId,
                    DocumentId = citation.DocumentId,
                    Relation = relation,
                    Now = DateTime.UtcNow.ToString("O")
                }, transaction, cancellationToken: cancellationToken));
        }

        return snippetIds.Count + documentCitations.Count;
    }

    private static string BuildAskMyVaultPrompt(string query, IReadOnlyList<DocumentChunkScore> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are AskMyVault, an evidence-grounded research assistant.");
        builder.AppendLine("Answer using only retrieved evidence and cite every claim with [DOC:<document_id>|chunk:<i>] and [SNIP:<id>] when available.");
        builder.AppendLine($"User query: {query}");
        builder.AppendLine($"Retrieved chunks: {chunks.Count}");
        return builder.ToString();
    }

    private static async Task<string> EnsureAskMyVaultAgentAsync(SqliteConnection connection, string workspaceId, string now, CancellationToken cancellationToken)
    {
        var existingId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM agent WHERE name = 'AskMyVault' ORDER BY created_at LIMIT 1", cancellationToken: cancellationToken));
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        var agentId = Guid.NewGuid().ToString();
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO agent (id, workspace_id, name, goal, instructions, allowed_tools_json, output_schema, evidence_policy, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, 'AskMyVault', 'Answer vault questions with citations', 'Use retrieved evidence and always cite sources.', '[""local_search"",""prompt_build""]', 'markdown', 'require citations', @Now, @Now)",
            new { Id = agentId, WorkspaceId = workspaceId, Now = now }, cancellationToken: cancellationToken));

        return agentId;
    }

    private static async Task<int> CreateCitationEvidenceLinksAsync(SqliteConnection connection, SqliteTransaction transaction, string workspaceId, string artifactId, string responseText, IReadOnlyList<DocumentChunkScore> selectedChunks, string now, CancellationToken cancellationToken)
    {
        var total = 0;

        foreach (Match match in Regex.Matches(responseText ?? string.Empty, @"\[SNIP:(?<id>[^\]]+)\]", RegexOptions.IgnoreCase))
        {
            var snippetId = match.Groups["id"].Value.Trim();
            if (string.IsNullOrWhiteSpace(snippetId))
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO evidence_link (id, workspace_id, from_entity_type, from_entity_id, to_entity_type, to_entity_id, relation, confidence, created_at)
                  VALUES (@Id, @WorkspaceId, 'artifact', @ArtifactId, 'snippet', @SnippetId, @Relation, NULL, @Now)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkspaceId = workspaceId,
                    ArtifactId = artifactId,
                    SnippetId = snippetId,
                    Relation = JsonSerializer.Serialize(new { kind = "snippet", locator = (string?)null, quote = (string?)null }),
                    Now = now
                }, transaction, cancellationToken: cancellationToken));
            total++;
        }

        foreach (Match match in Regex.Matches(responseText ?? string.Empty, @"\[DOC:(?<documentId>[^\]|]+)\|chunk:(?<chunkIndex>\d+)\]", RegexOptions.IgnoreCase))
        {
            var documentId = match.Groups["documentId"].Value.Trim();
            if (!int.TryParse(match.Groups["chunkIndex"].Value, out var chunkIndex) || string.IsNullOrWhiteSpace(documentId))
            {
                continue;
            }

            var quote = selectedChunks.FirstOrDefault(x => x.DocumentId == documentId && x.ChunkIndex == chunkIndex)?.Content;
            if (string.IsNullOrWhiteSpace(quote))
            {
                quote = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                    @"SELECT content
                        FROM document_text
                       WHERE document_id = @DocumentId
                         AND chunk_index = @ChunkIndex",
                    new { DocumentId = documentId, ChunkIndex = chunkIndex }, transaction, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO evidence_link (id, workspace_id, from_entity_type, from_entity_id, to_entity_type, to_entity_id, relation, confidence, created_at)
                  VALUES (@Id, @WorkspaceId, 'artifact', @ArtifactId, 'document', @DocumentId, @Relation, NULL, @Now)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkspaceId = workspaceId,
                    ArtifactId = artifactId,
                    DocumentId = documentId,
                    Relation = JsonSerializer.Serialize(new { kind = "document_locator", locator = $"chunk:{chunkIndex}", quote }),
                    Now = now
                }, transaction, cancellationToken: cancellationToken));
            total++;
        }

        return total;
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
        public string? DocumentOccurredAt { get; init; }
        public double Score { get; set; }
    }

    private sealed record PromptBuildResult(string Prompt, IReadOnlyList<string> CitationLabels);
}
