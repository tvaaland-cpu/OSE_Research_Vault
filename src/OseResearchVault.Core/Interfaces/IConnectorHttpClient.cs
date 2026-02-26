namespace OseResearchVault.Core.Interfaces;

public interface IConnectorHttpClient
{
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);
    Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default);
}
