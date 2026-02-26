using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AskMyVaultServiceTests
{
    [Fact]
    public async Task BuildPreviewAsync_MapsContextAndBuildsPromptWithCitations()
    {
        var fakeSearch = new FakeSearchService
        {
            Results =
            [
                new SearchResultRecord
                {
                    ResultType = "note",
                    EntityId = "n-1",
                    Title = "Debt maturity note",
                    MatchSnippet = "<mark>Debt</mark> maturity in 2027.",
                    CompanyName = "Acme Corp"
                },
                new SearchResultRecord
                {
                    ResultType = "document",
                    EntityId = "d-1",
                    Title = "10-K filing",
                    MatchSnippet = "Liquidity and debt coverage discussion."
                }
            ]
        };

        var service = new AskMyVaultService(fakeSearch, Microsoft.Extensions.Logging.Abstractions.NullLogger<AskMyVaultService>.Instance);
        var result = await service.BuildPreviewAsync(new AskMyVaultPreviewRequest
        {
            Query = "What is debt maturity risk?",
            CompanyId = "company-1",
            MaxContextItems = 5
        });

        Assert.Equal("company-1", fakeSearch.LastQuery?.CompanyId);
        Assert.Equal(2, result.ContextItems.Count);
        Assert.Equal("[note:n-1]", result.ContextItems[0].Citation);
        Assert.Equal("Debt maturity in 2027.", result.ContextItems[0].Excerpt);
        Assert.Contains("Question: What is debt maturity risk?", result.Prompt);
        Assert.Contains("[document:d-1]", result.Prompt);
    }

    private sealed class FakeSearchService : ISearchService
    {
        public IReadOnlyList<SearchResultRecord> Results { get; init; } = [];
        public SearchQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<SearchResultRecord>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Results);
        }

        public Task<IReadOnlyList<WorkspaceRecord>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkspaceRecord>>([]);
    }
}
