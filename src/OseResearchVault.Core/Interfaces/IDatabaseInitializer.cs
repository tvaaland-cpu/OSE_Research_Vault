namespace OseResearchVault.Core.Interfaces;

public interface IDatabaseInitializer
{
    Task<int> InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default);
}
