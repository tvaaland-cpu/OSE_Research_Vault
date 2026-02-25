namespace OseResearchVault.Core.Interfaces;

public interface ISecretStore
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);
}
