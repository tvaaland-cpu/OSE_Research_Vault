namespace OseResearchVault.Core.Interfaces;

public interface IHealthRepository
{
    Task<int> GetCompanyCountAsync(CancellationToken cancellationToken = default);
}
