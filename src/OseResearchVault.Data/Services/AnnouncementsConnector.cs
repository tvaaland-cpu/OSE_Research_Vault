using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed partial class AnnouncementsConnector(IAppSettingsService appSettingsService, IFtsSyncService ftsSyncService) : IConnector
{
    public string Id => "announcements-fetch";
    public string DisplayName => "Announcements Fetch";

    public async Task<ConnectorResult> RunAsync(ConnectorContext ctx, CancellationToken ct)
    {
        var result = new ConnectorResult();
        var settings = await appSettingsService.GetSettingsAsync(ct);
        var now = DateTime.UtcNow;

        var companyInfo = await ResolveCompanyAsync(settings.DatabaseFilePath, ctx, ct);
        if (companyInfo is null)
        {
            result.Errors.Add("Select a company before fetching announcements.");
            return result;
        }

        var days = ResolveDays(ctx.Settings);
        var manualUrls = ParseManualUrls(ctx.Settings);

        var candidates = new List<AnnouncementCandidate>();
        try
        {
            candidates.AddRange(await FetchYahooFeedAsync(companyInfo.Ticker, days, ctx.HttpClient, ct));
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Public feed unavailable; use manual URL import. {ex.Message}");
        }

        if (manualUrls.Count > 0)
        {
            candidates.AddRange(manualUrls.Select(url => new AnnouncementCandidate
            {
                Url = url,
                Title = "Manual announcement import",
                PublishedAt = null
            }));
        }

        if (candidates.Count == 0)
        {
            result.Errors.Add("No announcement URLs found.");
            return result;
        }

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dedupedCandidates = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Url) && seenUrls.Add(c.Url.Trim()))
            .ToList();

        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(ct);

        var added = 0;
        var skipped = 0;

        foreach (var candidate in dedupedCandidates)
        {
            ct.ThrowIfCancellationRequested();
            var url = candidate.Url.Trim();

            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM source WHERE workspace_id = @WorkspaceId AND url = @Url",
                new { WorkspaceId = ctx.WorkspaceId, Url = url }, cancellationToken: ct));

            if (exists > 0)
            {
                skipped++;
                continue;
            }

            try
            {
                var save = await SaveAnnouncementEvidenceAsync(connection, settings, ctx, companyInfo, candidate, now, ct);
                result.SourceIds.Add(save.SourceId);
                result.DocumentIds.Add(save.DocumentId);
                result.SourcesCreated++;
                result.DocumentsCreated++;
                added++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{url}: {ex.Message}");
            }
        }

        result.SourcesUpdated = skipped;
        return result;
    }

    private static int ResolveDays(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue("days", out var configured)
            && int.TryParse(configured, out var parsed)
            && parsed is >= 1 and <= 365)
        {
            return parsed;
        }

        return 30;
    }

    private static List<string> ParseManualUrls(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("manual_urls", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<CompanyLookup?> ResolveCompanyAsync(string databasePath, ConnectorContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ctx.CompanyId))
        {
            return null;
        }

        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<CompanyLookup>(new CommandDefinition(
            @"SELECT id AS Id, ticker AS Ticker, isin AS Isin, name AS Name
                 FROM company
                WHERE id = @CompanyId
                  AND workspace_id = @WorkspaceId",
            new { CompanyId = ctx.CompanyId, WorkspaceId = ctx.WorkspaceId }, cancellationToken: ct));
    }

    private static async Task<IReadOnlyList<AnnouncementCandidate>> FetchYahooFeedAsync(string? ticker, int days, IConnectorHttpClient client, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return [];
        }

        var feedUrl = $"https://feeds.finance.yahoo.com/rss/2.0/headline?s={Uri.EscapeDataString(ticker.Trim())}&region=US&lang=en-US";
        var xml = await client.GetStringAsync(feedUrl, ct);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        var cutoff = DateTime.UtcNow.AddDays(-Math.Abs(days));
        var doc = XDocument.Parse(xml);

        var items = doc.Descendants("item")
            .Select(item => new AnnouncementCandidate
            {
                Title = item.Element("title")?.Value?.Trim() ?? "Announcement",
                Url = item.Element("link")?.Value?.Trim() ?? string.Empty,
                PublishedAt = ParsePubDate(item.Element("pubDate")?.Value)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .Where(i => i.PublishedAt is null || i.PublishedAt >= cutoff)
            .ToList();

        return items;
    }

    private static DateTime? ParsePubDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, out var dt) ? dt.UtcDateTime : null;
    }

    private async Task<SnapshotSaveResult> SaveAnnouncementEvidenceAsync(SqliteConnection connection, AppSettings settings, ConnectorContext ctx, CompanyLookup company, AnnouncementCandidate candidate, DateTime now, CancellationToken ct)
    {
        var isPdf = candidate.Url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        var sourceId = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid().ToString();
        var nowText = now.ToString("O");
        var occurredAt = (candidate.PublishedAt ?? now).ToString("O");

        string title;
        string mimeType;
        string docType;
        string? filePath;
        string extractedText;

        if (isPdf)
        {
            var bytes = await ctx.HttpClient.GetBytesAsync(candidate.Url, ct);
            var snapshotDirectory = Path.Combine(settings.VaultStorageDirectory, "snapshots");
            Directory.CreateDirectory(snapshotDirectory);
            filePath = Path.Combine(snapshotDirectory, $"{documentId}.pdf");
            await File.WriteAllBytesAsync(filePath, bytes, ct);

            title = string.IsNullOrWhiteSpace(candidate.Title) ? Path.GetFileName(filePath) : candidate.Title;
            mimeType = "application/pdf";
            docType = "pdf";
            extractedText = ExtractPdfText(bytes);
        }
        else
        {
            var html = await ctx.HttpClient.GetStringAsync(candidate.Url, ct);
            title = TryGetTitle(html) ?? candidate.Title;
            title = string.IsNullOrWhiteSpace(title) ? candidate.Url : title;
            mimeType = "text/html";
            docType = "html";
            filePath = null;
            extractedText = StripHtml(html);
        }

        await using var tx = await connection.BeginTransactionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO source (id, workspace_id, company_id, name, source_type, url, publisher, fetched_at, retrieved_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, @Name, 'announcement', @Url, @Publisher, @Now, @Now, @Now, @Now)",
            new { Id = sourceId, WorkspaceId = ctx.WorkspaceId, CompanyId = company.Id, Name = title, Url = candidate.Url, Publisher = "Yahoo Finance RSS", Now = nowText }, transaction: tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document (id, workspace_id, source_id, company_id, title, doc_type, document_type, mime_type, file_path, imported_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @SourceId, @CompanyId, @Title, @DocType, @DocType, @MimeType, @FilePath, @Now, @Now, @Now)",
            new { Id = documentId, WorkspaceId = ctx.WorkspaceId, SourceId = sourceId, CompanyId = company.Id, Title = title, DocType = docType, MimeType = mimeType, FilePath = filePath, Now = nowText }, transaction: tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document_text (id, document_id, chunk_index, language, content, created_at, updated_at)
              VALUES (@Id, @DocumentId, 0, 'en', @Content, @Now, @Now)",
            new { Id = Guid.NewGuid().ToString(), DocumentId = documentId, Content = extractedText, Now = nowText }, transaction: tx, cancellationToken: ct));

        var payload = JsonSerializer.Serialize(new
        {
            confidence = "confirmed",
            source_id = sourceId,
            document_id = documentId,
            url = candidate.Url,
            retrieved_at = nowText
        });

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO event (id, workspace_id, company_id, event_type, title, payload_json, occurred_at, created_at)
              VALUES (@Id, @WorkspaceId, @CompanyId, 'announcement', @Title, @PayloadJson, @OccurredAt, @Now)",
            new { Id = Guid.NewGuid().ToString(), WorkspaceId = ctx.WorkspaceId, CompanyId = company.Id, Title = title, PayloadJson = payload, OccurredAt = occurredAt, Now = nowText }, transaction: tx, cancellationToken: ct));

        await tx.CommitAsync(ct);

        await ftsSyncService.UpsertDocumentTextAsync(documentId, title, extractedText, ct);

        return new SnapshotSaveResult { SourceId = sourceId, DocumentId = documentId };
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        var decoded = Encoding.UTF8.GetString(bytes);
        var textSegments = PdfTextRegex().Matches(decoded).Select(m => m.Groups[1].Value).ToList();
        if (textSegments.Count == 0)
        {
            var latin = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
            var fallback = WhitespaceRegex().Replace(latin, " ").Trim();
            return fallback.Length > 4000 ? fallback[..4000] : fallback;
        }

        var combined = string.Join(" ", textSegments);
        return WhitespaceRegex().Replace(combined, " ").Trim();
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false };
        return new SqliteConnection(builder.ToString());
    }

    private static string? TryGetTitle(string html)
    {
        var match = TitleRegex().Match(html ?? string.Empty);
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    private static string StripHtml(string html)
    {
        var noScript = ScriptStyleRegex().Replace(html ?? string.Empty, " ");
        var stripped = XmlTagRegex().Replace(noScript, " ");
        var decoded = System.Net.WebUtility.HtmlDecode(stripped);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    private sealed class CompanyLookup
    {
        public string Id { get; init; } = string.Empty;
        public string? Ticker { get; init; }
        public string? Isin { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class AnnouncementCandidate
    {
        public string Title { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public DateTime? PublishedAt { get; init; }
    }

    [GeneratedRegex("<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("<(script|style)[^>]*>[\\s\\S]*?</\\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex("\\((.*?)\\)\\s*Tj")]
    private static partial Regex PdfTextRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
