using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IReviewService
{
    Task<WeeklyReviewResult> GenerateWeeklyReviewAsync(string workspaceId, DateOnly asOfDate, CancellationToken cancellationToken = default);
    Task<QuarterlyReviewResult> GenerateQuarterlyCompanyReviewAsync(string workspaceId, string companyId, string periodLabel, CancellationToken cancellationToken = default);
}
