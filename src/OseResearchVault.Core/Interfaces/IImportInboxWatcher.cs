using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IImportInboxWatcher : IDisposable
{
    event EventHandler<ImportInboxEvent>? FileImported;
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
