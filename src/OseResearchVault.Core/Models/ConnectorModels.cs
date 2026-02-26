using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.Core.Models;

public sealed class ConnectorContext
{
    public required string WorkspaceId { get; init; }
    public string? CompanyId { get; init; }
    public required IConnectorHttpClient HttpClient { get; init; }
    public required IReadOnlyDictionary<string, string> Settings { get; init; }
    public required ILogger Logger { get; init; }
}

public sealed class ConnectorResult
{
    public int SourcesCreated { get; set; }
    public int SourcesUpdated { get; set; }
    public int DocumentsCreated { get; set; }
    public int DocumentsUpdated { get; set; }
    public List<string> SourceIds { get; } = [];
    public List<string> DocumentIds { get; } = [];
    public List<string> Errors { get; } = [];
}

public sealed class SnapshotSaveResult
{
    public required string SourceId { get; init; }
    public required string DocumentId { get; init; }
}
