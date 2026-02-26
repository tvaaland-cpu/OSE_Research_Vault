namespace OseResearchVault.Core.Models;

public sealed class RunDiffResult
{
    public required string TextDiff { get; init; }
    public required EvidenceCounts OriginalEvidence { get; init; }
    public required EvidenceCounts RerunEvidence { get; init; }
}

public sealed class EvidenceCounts
{
    public int LinkCount { get; init; }
    public int UniqueDocumentCount { get; init; }
    public int SnippetCount { get; init; }
}
