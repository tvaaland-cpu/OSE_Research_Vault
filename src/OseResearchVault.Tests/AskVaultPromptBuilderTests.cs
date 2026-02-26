using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AskVaultPromptBuilderTests
{
    [Fact]
    public void BuildAskVaultPrompt_IncludesCitationLabelsAndStrictContextRule()
    {
        var builder = new AskVaultPromptBuilder();
        var contextPack = new ContextPack
        {
            Items =
            [
                new ContextItem
                {
                    CitationLabel = "[SNIP:abc123]",
                    SourceDescription = "Q1 shareholder letter, page 2",
                    Content = "Revenue increased 24% year-over-year."
                },
                new ContextItem
                {
                    CitationLabel = "[DOC:def456]",
                    SourceDescription = "10-Q filing",
                    Content = "Gross margin improved by 180 basis points."
                }
            ]
        };

        var prompt = builder.BuildAskVaultPrompt("Summarize current momentum", "Acme Corp", contextPack);

        Assert.Contains("Use ONLY the provided context items.", prompt, StringComparison.Ordinal);
        Assert.Contains("[SNIP:abc123]", prompt, StringComparison.Ordinal);
        Assert.Contains("[DOC:def456]", prompt, StringComparison.Ordinal);
        Assert.Contains("Every factual claim must include one or more citations", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAskVaultPrompt_IsDeterministicForSameInputs()
    {
        var builder = new AskVaultPromptBuilder();
        var contextPack = new ContextPack
        {
            Items =
            [
                new ContextItem
                {
                    CitationLabel = "[SNIP:1]",
                    SourceDescription = "Interview notes",
                    Content = "Pipeline conversion accelerated in April."
                }
            ]
        };

        var first = builder.BuildAskVaultPrompt("What changed in Q2?", null, contextPack);
        var second = builder.BuildAskVaultPrompt("What changed in Q2?", null, contextPack);

        Assert.Equal(first, second);
    }
}
