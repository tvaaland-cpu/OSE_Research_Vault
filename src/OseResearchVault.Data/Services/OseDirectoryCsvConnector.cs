using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class OseDirectoryCsvConnector(IAppSettingsService appSettingsService, IFtsSyncService ftsSyncService) : IConnector
{
    public string Id => "ose-directory-import";
    public string DisplayName => "OSE Directory Import";

    public async Task<ConnectorResult> RunAsync(ConnectorContext ctx, CancellationToken ct)
    {
        var result = new ConnectorResult();
        var csvPath = ctx.Settings.TryGetValue("csv_path", out var configuredPath) ? configuredPath : null;
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            result.Errors.Add("Missing CSV path. Provide settings['csv_path'].");
            return result;
        }

        if (!File.Exists(csvPath))
        {
            result.Errors.Add($"CSV file not found: {csvPath}");
            return result;
        }

        var csv = await File.ReadAllTextAsync(csvPath, ct);
        var rows = ParseCsvRows(csv);
        if (rows.Count <= 1)
        {
            result.Errors.Add("CSV has no data rows.");
            return result;
        }

        var headerMap = BuildHeaderMap(rows[0]);
        var entries = rows.Skip(1)
            .Select(row => MapRow(row, headerMap))
            .Where(static row => !string.IsNullOrWhiteSpace(row.Name))
            .ToList();

        var settings = await appSettingsService.GetSettingsAsync(ct);
        await using var connection = OpenConnection(settings.DatabaseFilePath);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow.ToString("O");
        var sourceId = Guid.NewGuid().ToString();
        var documentId = Guid.NewGuid().ToString();

        var snapshotDirectory = Path.Combine(settings.VaultStorageDirectory, "snapshots");
        Directory.CreateDirectory(snapshotDirectory);
        var snapshotPath = Path.Combine(snapshotDirectory, $"ose-directory-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        await File.WriteAllTextAsync(snapshotPath, csv, Encoding.UTF8, ct);

        const string sourceName = "Oslo Stock Exchange directory import";
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO source (id, workspace_id, name, source_type, url, publisher, fetched_at, retrieved_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @Name, 'import', @Url, @Publisher, @Now, @Now, @Now, @Now)",
            new
            {
                Id = sourceId,
                WorkspaceId = ctx.WorkspaceId,
                Name = sourceName,
                Url = $"file://{Path.GetFullPath(csvPath)}",
                Publisher = "Oslo Bors",
                Now = now
            }, transaction: tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document (id, workspace_id, source_id, title, doc_type, document_type, mime_type, file_path, imported_at, created_at, updated_at)
              VALUES (@Id, @WorkspaceId, @SourceId, @Title, 'csv', 'csv', 'text/csv', @FilePath, @Now, @Now, @Now)",
            new
            {
                Id = documentId,
                WorkspaceId = ctx.WorkspaceId,
                SourceId = sourceId,
                Title = $"OSE Directory CSV ({Path.GetFileName(csvPath)})",
                FilePath = snapshotPath,
                Now = now
            }, transaction: tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO document_text (id, document_id, chunk_index, language, content, created_at, updated_at)
              VALUES (@Id, @DocumentId, 0, 'en', @Content, @Now, @Now)",
            new { Id = Guid.NewGuid().ToString(), DocumentId = documentId, Content = csv, Now = now }, transaction: tx, cancellationToken: ct));

        var existingCompanies = (await connection.QueryAsync<CompanyLookupRow>(new CommandDefinition(
            @"SELECT id, name, ticker, isin, summary
                FROM company
               WHERE workspace_id = @WorkspaceId",
            new { WorkspaceId = ctx.WorkspaceId }, transaction: tx, cancellationToken: ct))).ToList();

        foreach (var entry in entries)
        {
            var match = MatchCompany(existingCompanies, entry);
            if (match is null)
            {
                var companyId = Guid.NewGuid().ToString();
                var summary = string.IsNullOrWhiteSpace(entry.Exchange)
                    ? null
                    : $"Listed on {entry.Exchange}.";

                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO company (id, workspace_id, name, ticker, isin, summary, created_at, updated_at)
                      VALUES (@Id, @WorkspaceId, @Name, @Ticker, @Isin, @Summary, @Now, @Now)",
                    new
                    {
                        Id = companyId,
                        WorkspaceId = ctx.WorkspaceId,
                        Name = entry.Name,
                        Ticker = entry.Ticker,
                        Isin = entry.Isin,
                        Summary = summary,
                        Now = now
                    }, transaction: tx, cancellationToken: ct));

                existingCompanies.Add(new CompanyLookupRow { Id = companyId, Name = entry.Name, Ticker = entry.Ticker, Isin = entry.Isin, Summary = summary });
                continue;
            }

            var updatedName = string.IsNullOrWhiteSpace(match.Name) ? entry.Name : match.Name;
            var updatedTicker = string.IsNullOrWhiteSpace(match.Ticker) ? entry.Ticker : match.Ticker;
            var updatedIsin = string.IsNullOrWhiteSpace(match.Isin) ? entry.Isin : match.Isin;
            var updatedSummary = string.IsNullOrWhiteSpace(match.Summary) && !string.IsNullOrWhiteSpace(entry.Exchange)
                ? $"Listed on {entry.Exchange}."
                : match.Summary;

            if (!string.Equals(updatedName, match.Name, StringComparison.Ordinal)
                || !string.Equals(updatedTicker, match.Ticker, StringComparison.Ordinal)
                || !string.Equals(updatedIsin, match.Isin, StringComparison.Ordinal)
                || !string.Equals(updatedSummary, match.Summary, StringComparison.Ordinal))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"UPDATE company
                         SET name = @Name,
                             ticker = @Ticker,
                             isin = @Isin,
                             summary = @Summary,
                             updated_at = @Now
                       WHERE id = @Id",
                    new { Id = match.Id, Name = updatedName, Ticker = updatedTicker, Isin = updatedIsin, Summary = updatedSummary, Now = now }, transaction: tx, cancellationToken: ct));

                match.Name = updatedName;
                match.Ticker = updatedTicker;
                match.Isin = updatedIsin;
                match.Summary = updatedSummary;
            }
        }

        await tx.CommitAsync(ct);
        await ftsSyncService.UpsertDocumentTextAsync(documentId, $"OSE Directory CSV ({Path.GetFileName(csvPath)})", csv, ct);

        result.SourcesCreated = 1;
        result.DocumentsCreated = 1;
        result.SourceIds.Add(sourceId);
        result.DocumentIds.Add(documentId);
        return result;
    }

    private static CompanyLookupRow? MatchCompany(IEnumerable<CompanyLookupRow> existing, OseDirectoryRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.Isin))
        {
            var byIsin = existing.FirstOrDefault(c => string.Equals(c.Isin, row.Isin, StringComparison.OrdinalIgnoreCase));
            if (byIsin is not null)
            {
                return byIsin;
            }
        }

        if (!string.IsNullOrWhiteSpace(row.Ticker))
        {
            return existing.FirstOrDefault(c => string.Equals(c.Ticker, row.Ticker, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            var key = header[i].Trim();
            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
            {
                map[key] = i;
            }
        }

        return map;
    }

    private static OseDirectoryRow MapRow(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headerMap)
    {
        var name = GetByHeaders(row, headerMap, "name", "company", "issuer", "instrument");
        var ticker = GetByHeaders(row, headerMap, "ticker", "symbol");
        var isin = GetByHeaders(row, headerMap, "isin", "isin code");
        var exchange = GetByHeaders(row, headerMap, "exchange", "market", "venue");

        if (string.IsNullOrWhiteSpace(exchange))
        {
            exchange = "Oslo Stock Exchange (OSE)";
        }

        return new OseDirectoryRow
        {
            Name = name?.Trim() ?? string.Empty,
            Ticker = NormalizeNull(ticker),
            Isin = NormalizeNull(isin),
            Exchange = NormalizeNull(exchange)
        };
    }

    private static string? NormalizeNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetByHeaders(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headerMap, params string[] headers)
    {
        foreach (var header in headers)
        {
            if (headerMap.TryGetValue(header, out var index) && index < row.Count)
            {
                var value = row[index]?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static List<IReadOnlyList<string>> ParseCsvRows(string csv)
    {
        var rows = new List<IReadOnlyList<string>>();
        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }

            if (c == ',')
            {
                current.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (c == '\n')
            {
                current.Add(field.ToString());
                field.Clear();
                rows.Add(current);
                current = new List<string>();
                continue;
            }

            if (c != '\r')
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            rows.Add(current);
        }

        return rows;
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true };
        return new SqliteConnection(builder.ToString());
    }

    private sealed class OseDirectoryRow
    {
        public string Name { get; init; } = string.Empty;
        public string? Ticker { get; init; }
        public string? Isin { get; init; }
        public string? Exchange { get; init; }
    }

    private sealed class CompanyLookupRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Ticker { get; set; }
        public string? Isin { get; set; }
        public string? Summary { get; set; }
    }
}
