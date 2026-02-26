using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IReviewService
{
    Task<WeeklyReviewResult> GenerateWeeklyReviewAsync(string workspaceId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
