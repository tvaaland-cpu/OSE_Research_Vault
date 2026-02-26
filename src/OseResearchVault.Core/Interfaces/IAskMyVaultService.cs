using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IAskMyVaultService
{
    Task<AskMyVaultPreviewResult> BuildPreviewAsync(AskMyVaultPreviewRequest request, CancellationToken cancellationToken = default);
}
