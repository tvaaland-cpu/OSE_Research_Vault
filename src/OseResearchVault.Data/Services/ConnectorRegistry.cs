using Microsoft.Extensions.Logging;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class ConnectorRegistry(IEnumerable<IConnector> connectors, ILogger<ConnectorRegistry> logger) : IConnectorRegistry
{
    private readonly List<IConnector> _connectors = connectors.ToList();

    public IReadOnlyList<IConnector> GetConnectors() => _connectors;

    public async Task<ConnectorResult> RunAsync(string connectorId, ConnectorContext context, CancellationToken cancellationToken = default)
    {
        var connector = _connectors.FirstOrDefault(c => string.Equals(c.Id, connectorId, StringComparison.OrdinalIgnoreCase));
        if (connector is null)
        {
            return new ConnectorResult { Errors = { $"Connector '{connectorId}' was not found." } };
        }

        logger.LogInformation("Running connector {ConnectorId}", connector.Id);
        return await connector.RunAsync(context, cancellationToken);
    }
}
