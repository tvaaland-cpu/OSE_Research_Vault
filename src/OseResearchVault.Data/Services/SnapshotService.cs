using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed partial class SnapshotService(IAppSettingsService appSettingsService, IConnectorHttpClient connectorHttpClient, IFtsSyncService ftsSyncService) : ISnapshotService
{
    public async Task<SnapshotSaveResult> SaveUrlSnapshotAsync(string url, string workspaceId, string? companyId, string snapshotType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }

        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var normalizedType = snapshotType.Trim().ToLowerInvariant();
        var documentId = Guid.NewGuid().ToString();
        var sourceId = Guid.NewGuid().ToString();

        string? extractedText = null;
        string title;
        string docType;

        if (normalizedType == "html")
        {
            var html = await connectorHttpClient.GetStringAsync(url, cancellationToken);
            title = TryGetTitle(html) ?? url;
            docType = "html";
            extractedText = StripHtml(html);

            await InsertRecordsAsync(settings.DatabaseFilePath, sourceId, documentId, workspaceId, companyId, url, now, docType, title, "text/html", null, extractedText, cancellationToken);
            await ftsSyncService.UpsertDocumentTextAsync(documentId, title, extractedText ?? string.Empty, cancellationToken);
        }
        else if (normalizedType == "pdf")
        {
            var bytes = await connectorHttpClient.GetBytesAsync(url, cancellationToken);
            var snapshotsDirectory = Path.Combine(settings.VaultStorageDirectory, "snapshots");
            Directory.CreateDirectory(snapshotsDirectory);
            var filePath = Path.Combine(snapshotsDirectory, $"{documentId}.pdf");
            await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);

            title = Path.GetFileNameWithoutExtension(filePath);
            docType = "pdf";
            await InsertRecordsAsync(settings.DatabaseFilePath, sourceId, documentId, workspaceId, companyId, url, now, docType, title, "application/pdf", filePath, null, cancellationToken);
        }
        else if (normalizedType == "screenshot")
        {
            throw new NotSupportedException("Screenshot snapshots are not implemented yet.");
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(snapshotType), snapshotType, "Unsupported snapshot type.");
        }

        return new SnapshotSaveResult { SourceId = sourceId, DocumentId = documentId };
    }

    private static async Task InsertRecordsAsync(string databasePath, string sourceId, string documentId, string workspaceId, string? companyId, string url, string now, string docType, string title, string mimeType, string? filePath, string? extractedText, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO source (id, workspace_id, company_id, name, source_type, url, fetched_at, retrieved_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Name, 'url', @Url, @Now, @Now, @Now, @Now)",
            new { Id = sourceId, WorkspaceId = workspaceId, CompanyId = string.IsNullOrWhiteSpace(companyId) ? null : companyId, Name = title, Url = url, Now = now }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document (id, workspace_id, source_id, company_id, title, doc_type, document_type, mime_type, file_path, imported_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @SourceId, @CompanyId, @Title, @DocType, @DocType, @MimeType, @FilePath, @Now, @Now, @Now)",
            new
            {
                Id = documentId,
                WorkspaceId = workspaceId,
                SourceId = sourceId,
                CompanyId = string.IsNullOrWhiteSpace(companyId) ? null : companyId,
                Title = title,
                DocType = docType,
                MimeType = mimeType,
                FilePath = filePath,
                Now = now
            }, cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO document_text (id, document_id, chunk_index, language, content, created_at, updated_at)
                  VALUES (@Id, @DocumentId, 0, 'en', @Content, @Now, @Now)",
                new { Id = Guid.NewGuid().ToString(), DocumentId = documentId, Content = extractedText, Now = now }, cancellationToken: cancellationToken));
        }
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true };
        return new SqliteConnection(builder.ToString());
    }

    private static string? TryGetTitle(string html)
    {
        var match = TitleRegex().Match(html);
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    private static string StripHtml(string html)
    {
        var noScript = ScriptStyleRegex().Replace(html, " ");
        var stripped = XmlTagRegex().Replace(noScript, " ");
        var decoded = System.Net.WebUtility.HtmlDecode(stripped);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    [GeneratedRegex("<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("<(script|style)[^>]*>[\\s\\S]*?</\\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
