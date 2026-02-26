using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class RunDiffService
{
    public RunDiffResult Compare(string? originalText, string? rerunText, IReadOnlyList<EvidenceLink> originalLinks, IReadOnlyList<EvidenceLink> rerunLinks)
    {
        return new RunDiffResult
        {
            TextDiff = BuildLineDiff(originalText, rerunText),
            OriginalEvidence = BuildCounts(originalLinks),
            RerunEvidence = BuildCounts(rerunLinks)
        };
    }

    private static EvidenceCounts BuildCounts(IReadOnlyList<EvidenceLink> links)
    {
        return new EvidenceCounts
        {
            LinkCount = links.Count,
            UniqueDocumentCount = links.Where(x => !string.IsNullOrWhiteSpace(x.DocumentId)).Select(x => x.DocumentId!).Distinct(StringComparer.Ordinal).Count(),
            SnippetCount = links.Where(x => !string.IsNullOrWhiteSpace(x.SnippetId)).Select(x => x.SnippetId!).Distinct(StringComparer.Ordinal).Count()
        };
    }

    private static string BuildLineDiff(string? originalText, string? rerunText)
    {
        var originalLines = SplitLines(originalText);
        var rerunLines = SplitLines(rerunText);
        var max = Math.Max(originalLines.Length, rerunLines.Length);
        if (max == 0)
        {
            return "(Both artifacts are empty.)";
        }

        var output = new List<string>(max);
        for (var index = 0; index < max; index++)
        {
            var left = index < originalLines.Length ? originalLines[index] : null;
            var right = index < rerunLines.Length ? rerunLines[index] : null;
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                output.Add($"  {left}");
                continue;
            }

            if (left is not null)
            {
                output.Add($"- {left}");
            }

            if (right is not null)
            {
                output.Add($"+ {right}");
            }
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }
}
