using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IMetricService
{
    Task<MetricUpsertResult> UpsertMetricAsync(MetricUpsertRequest request, MetricConflictResolution conflictResolution = MetricConflictResolution.CreateOnly, CancellationToken cancellationToken = default);
}
