using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Repositories;

public sealed class SqliteEvidenceLinkRepository(IAppSettingsService appSettingsService) : IEvidenceLinkRepository
{
    public async Task<EvidenceLink> CreateEvidenceLinkAsync(string artifactId, string? snippetId, string? documentId, string? locator, string? quote, double? relevanceScore, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var workspaceId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT workspace_id FROM artifact WHERE id = @ArtifactId",
            new { ArtifactId = artifactId }, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("Artifact not found.");
        }

        var isSnippetLink = !string.IsNullOrWhiteSpace(snippetId);
        var toEntityType = isSnippetLink ? "snippet" : "document";
        var toEntityId = isSnippetLink ? snippetId! : documentId!;

        var relation = JsonSerializer.Serialize(new
        {
            kind = isSnippetLink ? "snippet" : "document_locator",
            locator,
            quote
        });

        var now = DateTime.UtcNow.ToString("O");
        var id = Guid.NewGuid().ToString();

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO evidence_link (id, workspace_id, from_entity_type, from_entity_id, to_entity_type, to_entity_id, relation, confidence, created_at)
              VALUES (@Id, @WorkspaceId, 'artifact', @ArtifactId, @ToEntityType, @ToEntityId, @Relation, @Confidence, @Now)",
            new
            {
                Id = id,
                WorkspaceId = workspaceId,
                ArtifactId = artifactId,
                ToEntityType = toEntityType,
                ToEntityId = toEntityId,
                Relation = relation,
                Confidence = relevanceScore,
                Now = now
            }, cancellationToken: cancellationToken));

        return new EvidenceLink
        {
            Id = id,
            WorkspaceId = workspaceId,
            ArtifactId = artifactId,
            SnippetId = snippetId,
            DocumentId = documentId,
            Locator = locator,
            Quote = quote,
            RelevanceScore = relevanceScore,
            CreatedAt = now
        };
    }

    public async Task<IReadOnlyList<EvidenceLink>> ListEvidenceLinksByArtifactAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<EvidenceLinkRow>(new CommandDefinition(
            @"SELECT el.id,
                     el.workspace_id AS WorkspaceId,
                     el.from_entity_id AS ArtifactId,
                     el.to_entity_type AS ToEntityType,
                     el.to_entity_id AS ToEntityId,
                     el.relation AS Relation,
                     el.confidence AS RelevanceScore,
                     el.created_at AS CreatedAt,
                     s.quote_text AS SnippetText,
                     d.title AS DocumentTitle,
                     d.company_id AS CompanyId,
                     c.name AS CompanyName
                FROM evidence_link el
                LEFT JOIN snippet s ON el.to_entity_type = 'snippet' AND s.id = el.to_entity_id
                LEFT JOIN document d ON d.id = CASE WHEN el.to_entity_type = 'snippet' THEN s.document_id ELSE el.to_entity_id END
                LEFT JOIN company c ON c.id = d.company_id
               WHERE el.from_entity_type = 'artifact'
                 AND el.from_entity_id = @ArtifactId
            ORDER BY el.created_at DESC",
            new { ArtifactId = artifactId }, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    public async Task DeleteEvidenceLinkAsync(string evidenceLinkId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM evidence_link WHERE id = @Id",
            new { Id = evidenceLinkId }, cancellationToken: cancellationToken));
    }

    private static EvidenceLink Map(EvidenceLinkRow row)
    {
        var metadata = ParseRelation(row.Relation);

        return new EvidenceLink
        {
            Id = row.Id,
            WorkspaceId = row.WorkspaceId,
            ArtifactId = row.ArtifactId,
            SnippetId = row.ToEntityType == "snippet" ? row.ToEntityId : null,
            DocumentId = row.ToEntityType == "document" ? row.ToEntityId : null,
            Locator = metadata.Locator,
            Quote = metadata.Quote,
            RelevanceScore = row.RelevanceScore,
            CreatedAt = row.CreatedAt,
            SnippetText = row.SnippetText,
            DocumentTitle = row.DocumentTitle,
            CompanyId = row.CompanyId,
            CompanyName = row.CompanyName
        };
    }

    private static (string? Locator, string? Quote) ParseRelation(string relation)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<RelationPayload>(relation);
            return (payload?.Locator, payload?.Quote);
        }
        catch
        {
            return (null, null);
        }
    }

    private static SqliteConnection OpenConnection(string databasePath)
        => new(new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString());

    private sealed class EvidenceLinkRow
    {
        public string Id { get; init; } = string.Empty;
        public string WorkspaceId { get; init; } = string.Empty;
        public string ArtifactId { get; init; } = string.Empty;
        public string ToEntityType { get; init; } = string.Empty;
        public string ToEntityId { get; init; } = string.Empty;
        public string Relation { get; init; } = string.Empty;
        public double? RelevanceScore { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string? SnippetText { get; init; }
        public string? DocumentTitle { get; init; }
        public string? CompanyId { get; init; }
        public string? CompanyName { get; init; }
    }

    private sealed class RelationPayload
    {
        public string? Locator { get; init; }
        public string? Quote { get; init; }
    }
}
