using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using UglyToad.PdfPig;

namespace OseResearchVault.Data.Services;

public sealed partial class SqliteDocumentImportService(
    IAppSettingsService appSettingsService,
    IFtsSyncService ftsSyncService,
    ILogger<SqliteDocumentImportService> logger) : IDocumentImportService
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".pdf", ".pptx", ".docx", ".xlsx", ".txt", ".html", ".htm", ".png", ".jpg", ".jpeg"
    ];

    public async Task<IReadOnlyList<DocumentImportResult>> ImportFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        var results = new List<DocumentImportResult>();
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        var workspaceId = await EnsureWorkspaceAsync(settings.DatabaseFilePath, cancellationToken);

        var storageDirectory = Path.Combine(settings.VaultStorageDirectory, "documents");
        Directory.CreateDirectory(storageDirectory);

        foreach (var rawPath in filePaths.Where(static p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var filePath = Path.GetFullPath(rawPath);
                if (!File.Exists(filePath))
                {
                    results.Add(new DocumentImportResult { FilePath = rawPath, Succeeded = false, ErrorMessage = "File not found." });
                    continue;
                }

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (!SupportedExtensions.Contains(extension))
                {
                    results.Add(new DocumentImportResult { FilePath = filePath, Succeeded = false, ErrorMessage = $"Unsupported file type: {extension}" });
                    continue;
                }

                var hash = await ComputeSha256Async(filePath, cancellationToken);
                var storedName = string.IsNullOrWhiteSpace(extension) ? hash : $"{hash}{extension}";
                var storedPath = Path.Combine(storageDirectory, storedName);

                if (!File.Exists(storedPath))
                {
                    File.Copy(filePath, storedPath);
                }

                var importedAt = DateTime.UtcNow.ToString("O");
                var documentId = Guid.NewGuid().ToString();
                var extractedText = await TryExtractTextAsync(filePath, extension, cancellationToken);

                await using var connection = OpenConnection(settings.DatabaseFilePath);
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO document (id, workspace_id, title, doc_type, document_type, file_path, content_hash, imported_at, mime_type, created_at, updated_at)
                      VALUES (@Id, @WorkspaceId, @Title, @DocType, @DocType, @FilePath, @ContentHash, @ImportedAt, @MimeType, @Now, @Now)",
                    new
                    {
                        Id = documentId,
                        WorkspaceId = workspaceId,
                        Title = Path.GetFileNameWithoutExtension(filePath),
                        DocType = extension.TrimStart('.').ToUpperInvariant(),
                        FilePath = storedPath,
                        ContentHash = hash,
                        ImportedAt = importedAt,
                        MimeType = GetMimeType(extension),
                        Now = importedAt
                    }, cancellationToken: cancellationToken));

                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        @"INSERT INTO document_text (id, document_id, chunk_index, language, content, created_at, updated_at)
                          VALUES (@Id, @DocumentId, 0, 'en', @Content, @Now, @Now)",
                        new
                        {
                            Id = Guid.NewGuid().ToString(),
                            DocumentId = documentId,
                            Content = extractedText,
                            Now = importedAt
                        }, cancellationToken: cancellationToken));
                }

                await ftsSyncService.UpsertDocumentTextAsync(
                    documentId,
                    Path.GetFileNameWithoutExtension(filePath),
                    extractedText ?? string.Empty,
                    cancellationToken);

                results.Add(new DocumentImportResult { FilePath = filePath, Succeeded = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to import file {FilePath}", rawPath);
                results.Add(new DocumentImportResult { FilePath = rawPath, Succeeded = false, ErrorMessage = "Failed to import file. Please verify the file is valid and try again." });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<DocumentRecord>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DocumentRecord>(new CommandDefinition(
            @"SELECT d.id,
                     d.workspace_id AS WorkspaceId,
                     d.title,
                     COALESCE(d.doc_type, d.document_type, '') AS DocType,
                     d.company_id AS CompanyId,
                     c.name AS CompanyName,
                     d.published_at AS PublishedAt,
                     COALESCE(d.imported_at, d.created_at) AS ImportedAt,
                     d.file_path AS FilePath,
                     COALESCE(d.content_hash, '') AS ContentHash,
                     NULL AS ExtractedText
                FROM document d
                LEFT JOIN company c ON c.id = d.company_id
            ORDER BY COALESCE(d.imported_at, d.created_at) DESC" ,
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<DocumentRecord?> GetDocumentDetailsAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DocumentRecord>(new CommandDefinition(
            @"SELECT d.id,
                     d.workspace_id AS WorkspaceId,
                     d.title,
                     COALESCE(d.doc_type, d.document_type, '') AS DocType,
                     d.company_id AS CompanyId,
                     c.name AS CompanyName,
                     d.published_at AS PublishedAt,
                     COALESCE(d.imported_at, d.created_at) AS ImportedAt,
                     d.file_path AS FilePath,
                     COALESCE(d.content_hash, '') AS ContentHash,
                     (SELECT group_concat(dt.content, char(10) || char(10))
                        FROM document_text dt
                       WHERE dt.document_id = d.id
                    ORDER BY dt.chunk_index) AS ExtractedText
                FROM document d
                LEFT JOIN company c ON c.id = d.company_id
               WHERE d.id = @DocumentId", new { DocumentId = documentId }, cancellationToken: cancellationToken));

        return row;
    }

    public async Task UpdateDocumentCompanyAsync(string documentId, string? companyId, CancellationToken cancellationToken = default)
    {
        var settings = await appSettingsService.GetSettingsAsync(cancellationToken);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE document
                 SET company_id = @CompanyId,
                     updated_at = @Now
               WHERE id = @DocumentId",
            new { DocumentId = documentId, CompanyId = string.IsNullOrWhiteSpace(companyId) ? null : companyId, Now = DateTime.UtcNow.ToString("O") },
            cancellationToken: cancellationToken));
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static async Task<string?> TryExtractTextAsync(string filePath, string extension, CancellationToken cancellationToken)
    {
        return extension switch
        {
            ".txt" => await File.ReadAllTextAsync(filePath, cancellationToken),
            ".html" or ".htm" => StripHtml(await File.ReadAllTextAsync(filePath, cancellationToken)),
            ".docx" => ExtractDocxText(filePath),
            ".pdf" => ExtractPdfText(filePath),
            _ => null
        };
    }

    private static string ExtractPdfText(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString().Trim();
    }

    private static string ExtractDocxText(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            return string.Empty;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();

        var withBreaks = xml.Replace("</w:p>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</w:tr>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</w:tc>", " ", StringComparison.OrdinalIgnoreCase);

        var text = XmlTagRegex().Replace(withBreaks, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return WhitespaceRegex().Replace(text, " ").Replace(" \n ", "\n").Trim();
    }

    private static string StripHtml(string html)
    {
        var noScript = ScriptStyleRegex().Replace(html, " ");
        var stripped = XmlTagRegex().Replace(noScript, " ");
        var decoded = System.Net.WebUtility.HtmlDecode(stripped);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    private static string GetMimeType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".txt" => "text/plain",
        ".html" or ".htm" => "text/html",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };

    private static SqliteConnection OpenConnection(string databasePath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString());
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

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyleRegex();
}
