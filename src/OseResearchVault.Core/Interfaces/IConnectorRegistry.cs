using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IConnectorRegistry
{
    IReadOnlyList<IConnector> GetConnectors();
    Task<ConnectorResult> RunAsync(string connectorId, ConnectorContext context, CancellationToken cancellationToken = default);
}
