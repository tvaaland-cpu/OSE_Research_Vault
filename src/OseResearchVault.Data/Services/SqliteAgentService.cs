using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class SqliteAgentService(IAppSettingsService appSettingsService) : IAgentService
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

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO agent_run (id, agent_id, workspace_id, company_id, status, query_text, selected_document_ids_json, started_at, finished_at)
              VALUES (@Id, @AgentId, @WorkspaceId, @CompanyId, 'success', @Query, @SelectedDocumentIdsJson, @Now, @Now)",
            new
            {
                Id = runId,
                request.AgentId,
                WorkspaceId = workspaceId,
                CompanyId = Clean(request.CompanyId),
                Query = Clean(request.Query),
                SelectedDocumentIdsJson = JsonSerializer.Serialize(request.SelectedDocumentIds.Distinct()),
                Now = now
            },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO artifact (id, workspace_id, agent_run_id, artifact_type, title, content, content_format, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @RunId, 'agent_output', @Title, '', 'text', @Now, @Now)",
            new { Id = artifactId, WorkspaceId = workspaceId, RunId = runId, Title = "Run Output (placeholder)", Now = now },
            transaction,
            cancellationToken: cancellationToken));

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
}
