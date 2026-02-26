using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IConnector
{
    string Id { get; }
    string DisplayName { get; }
    Task<ConnectorResult> RunAsync(ConnectorContext ctx, CancellationToken ct);
}
