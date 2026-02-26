using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IMetricService
{
    Task<string> CreateMetricAsync(MetricCreateRequest request, CancellationToken cancellationToken = default);
}
