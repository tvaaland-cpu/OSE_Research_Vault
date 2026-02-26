using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed partial class SqliteMemoPublishService(IAppSettingsService appSettingsService, IRedactionService redactionService, IFtsSyncService ftsSyncService) : IMemoPublishService
{
    public bool SupportsPdf => false;

    public async Task<MemoPublishResult> PublishAsync(MemoPublishRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Format == MemoPublishFormat.Pdf)
        {
            throw new NotSupportedException("PDF publish is not available in this build.");
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);
        var publishRoot = Path.Combine(settings.VaultStorageDirectory, "published-memos");
        Directory.CreateDirectory(publishRoot);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var companyPart = SanitizeFileNamePart(string.IsNullOrWhiteSpace(request.CompanyName) ? "Company" : request.CompanyName!);
        var fileName = $"{companyPart}_Memo_{today}.md";
        var outputPath = Path.Combine(publishRoot, fileName);

        var redactionResult = redactionService.Redact(request.NoteContent, request.RedactionOptions);
        var content = BuildMarkdown(redactionResult.RedactedText, request, redactionResult.Hits);
        await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, cancellationToken);

        var now = DateTime.UtcNow.ToString("O");
        var sourceId = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid().ToString();
        var hash = await ComputeSha256Async(outputPath, cancellationToken);

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO source (id, workspace_id, company_id, name, source_type, url, fetched_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Name, 'file', @Url, @Now, @Now, @Now)",
            new { Id = sourceId, WorkspaceId = workspaceId, CompanyId = EmptyToNull(request.CompanyId), Name = Path.GetFileNameWithoutExtension(fileName), Url = outputPath, Now = now }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document (id, workspace_id, source_id, company_id, title, doc_type, document_type, mime_type, file_path, imported_at, content_hash, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @SourceId, @CompanyId, @Title, 'md', 'md', 'text/markdown', @FilePath, @Now, @Hash, @Now, @Now)",
            new
            {
                Id = documentId,
                WorkspaceId = workspaceId,
                SourceId = sourceId,
                CompanyId = EmptyToNull(request.CompanyId),
                Title = Path.GetFileNameWithoutExtension(fileName),
                FilePath = outputPath,
                Now = now,
                Hash = hash
            }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document_text (id, document_id, chunk_index, language, content, created_at, updated_at)
              VALUES (@Id, @DocumentId, 0, 'en', @Content, @Now, @Now)",
            new { Id = Guid.NewGuid().ToString(), DocumentId = documentId, Content = content, Now = now }, cancellationToken: cancellationToken));

        await ftsSyncService.UpsertDocumentTextAsync(documentId, Path.GetFileNameWithoutExtension(fileName), content, cancellationToken);

        return new MemoPublishResult { OutputFilePath = outputPath, SourceId = sourceId, DocumentId = documentId };
    }

    private static string BuildMarkdown(string redactedContent, MemoPublishRequest request, IReadOnlyList<RedactionHit> hits)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {request.NoteTitle}");
        builder.AppendLine();
        builder.AppendLine(redactedContent.Trim());

        if (request.IncludeCitationsList)
        {
            var citations = CitationRegex().Matches(redactedContent).Select(static m => m.Value).Distinct(StringComparer.Ordinal).ToList();
            builder.AppendLine();
            builder.AppendLine("## Appendix: Citations");
            if (citations.Count == 0)
            {
                builder.AppendLine("- None detected");
            }
            else
            {
                foreach (var citation in citations)
                {
                    builder.AppendLine($"- `{citation}`");
                }
            }
        }

        if (request.IncludeEvidenceExcerpts)
        {
            var lines = redactedContent
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(line => CitationRegex().IsMatch(line))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            builder.AppendLine();
            builder.AppendLine("## Appendix: Evidence Excerpts");
            if (lines.Count == 0)
            {
                builder.AppendLine("- None detected");
            }
            else
            {
                foreach (var line in lines)
                {
                    builder.AppendLine($"> {line}");
                    builder.AppendLine();
                }
            }
        }

        if (hits.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Redaction Summary");
            builder.AppendLine($"- Replacements applied: {hits.Count}");
        }

        return builder.ToString();
    }

    private static string SanitizeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        cleaned = WhitespaceRegex().Replace(cleaned, "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "Company" : cleaned;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static async Task<string> EnsureWorkspaceAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync(cancellationToken);
        var workspaceId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT id FROM workspace ORDER BY created_at LIMIT 1", cancellationToken: cancellationToken));
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaceId;
        }

        workspaceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("O");
        await connection.ExecuteAsync(new CommandDefinition("INSERT INTO workspace (id, name, created_at, updated_at) VALUES (@Id, 'Default', @Now, @Now)", new { Id = workspaceId, Now = now }, cancellationToken: cancellationToken));
        return workspaceId;
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true };
        return new SqliteConnection(builder.ToString());
    }

    [GeneratedRegex("\\[DOC:[^\\]]+\\]", RegexOptions.Compiled)]
    private static partial Regex CitationRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
