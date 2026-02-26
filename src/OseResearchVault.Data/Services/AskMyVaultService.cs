using System.Text;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class AskMyVaultService(ISearchService searchService) : IAskMyVaultService
{
    public async Task<AskMyVaultPreviewResult> BuildPreviewAsync(AskMyVaultPreviewRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new AskMyVaultPreviewResult
            {
                Prompt = "",
                ContextItems = []
            };
        }

        var maxItems = Math.Max(1, request.MaxContextItems);
        var matches = await searchService.SearchAsync(new SearchQuery
        {
            QueryText = request.Query.Trim(),
            CompanyId = string.IsNullOrWhiteSpace(request.CompanyId) ? null : request.CompanyId,
            Type = "All"
        }, cancellationToken);

        var contextItems = matches
            .Take(maxItems)
            .Select(MapContextItem)
            .ToList();

        return new AskMyVaultPreviewResult
        {
            ContextItems = contextItems,
            Prompt = BuildPrompt(request.Query.Trim(), request.CompanyId, contextItems)
        };
    }

    private static AskMyVaultContextItem MapContextItem(SearchResultRecord match)
    {
        var type = string.IsNullOrWhiteSpace(match.ResultType) ? "unknown" : match.ResultType.ToLowerInvariant();
        return new AskMyVaultContextItem
        {
            ResultType = type,
            EntityId = match.EntityId,
            Title = match.Title,
            Excerpt = CleanSnippet(match.MatchSnippet),
            CompanyName = match.CompanyName,
            Citation = $"[{type}:{match.EntityId}]"
        };
    }

    private static string BuildPrompt(string query, string? companyId, IReadOnlyList<AskMyVaultContextItem> contextItems)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are Ask My Vault.");
        builder.AppendLine($"Scope: {(string.IsNullOrWhiteSpace(companyId) ? "Global" : $"Company ({companyId})")}");
        builder.AppendLine($"Question: {query}");
        builder.AppendLine();
        builder.AppendLine("Retrieved context:");

        if (contextItems.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var item in contextItems)
            {
                builder.AppendLine($"- {item.Citation} ({item.ResultType}) {item.Title}");
                if (!string.IsNullOrWhiteSpace(item.Excerpt))
                {
                    builder.AppendLine($"  Excerpt: {item.Excerpt}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("Draft an answer and cite sources using the citations exactly as provided above.");
        return builder.ToString();
    }

    private static string CleanSnippet(string? snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
        {
            return string.Empty;
        }

        return snippet
            .Replace("<mark>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</mark>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
