using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultRecord>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceRecord>> GetWorkspacesAsync(CancellationToken cancellationToken = default);
}
