using System.Text;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class AskVaultPromptBuilder : IPromptBuilder
{
    public string BuildAskVaultPrompt(string query, string? companyName, ContextPack contextPack, AskVaultStyleOptions? styleOptions = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(contextPack);

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        var options = styleOptions ?? new AskVaultStyleOptions();
        var normalizedCompany = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();

        var builder = new StringBuilder();
        builder.AppendLine("You are Ask-My-Vault.");
        builder.AppendLine("Use ONLY the provided context items. Do not use outside knowledge, assumptions, or unstated facts.");
        builder.AppendLine("Every factual claim must include one or more citations using the exact citation labels from context (for example: [SNIP:...] [DOC:...]).");
        builder.AppendLine("If evidence is insufficient, explicitly say that evidence is insufficient and list what information is missing.");
        builder.AppendLine();
        builder.AppendLine($"User query: {normalizedQuery}");
        if (normalizedCompany is not null)
        {
            builder.AppendLine($"Company scope: {normalizedCompany}");
        }

        builder.AppendLine();
        builder.AppendLine("Output format (must follow exactly):");
        builder.AppendLine(options.PreferBulletedAnswer
            ? "1) Answer (bulleted or short paragraphs, concise)."
            : "1) Answer (short paragraphs, concise).");
        builder.AppendLine("2) Evidence (list of citations used with one-line description for each citation).");
        if (options.IncludeGapsSection)
        {
            builder.AppendLine("3) Gaps / Follow-ups (optional; include only when evidence is missing or uncertain).");
        }

        builder.AppendLine();
        builder.AppendLine("Provided context items:");
        if (contextPack.Items.Count == 0)
        {
            builder.AppendLine("- (none provided)");
        }
        else
        {
            foreach (var item in contextPack.Items)
            {
                builder.Append("- ")
                    .Append(item.CitationLabel)
                    .Append(" | ")
                    .Append(item.SourceDescription)
                    .AppendLine();
                builder.Append("  ")
                    .AppendLine(item.Content);
            }
        }

        return builder.ToString().TrimEnd();
    }
}
