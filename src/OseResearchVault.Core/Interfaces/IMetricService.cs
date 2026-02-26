using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IMetricService
{
    Task<Metric> CreateMetricAsync(
        string workspaceId,
        string companyId,
        string metricName,
        string period,
        double value,
        string? unit,
        string? currency,
        string snippetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Metric>> ListMetricsByCompanyAsync(string workspaceId, string companyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Metric>> ListMetricsByCompanyAndNameAsync(string workspaceId, string companyId, string metricName, CancellationToken cancellationToken = default);

    Task<Metric?> UpdateMetricAsync(
        string workspaceId,
        string metricId,
        string metricName,
        string period,
        double value,
        string? unit,
        string? currency,
        CancellationToken cancellationToken = default);

    Task DeleteMetricAsync(string workspaceId, string metricId, CancellationToken cancellationToken = default);
}
