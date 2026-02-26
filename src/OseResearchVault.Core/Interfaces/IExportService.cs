using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IExportService
{
    Task ExportCompanyResearchPackAsync(string workspaceId, string companyId, string outputFolder, RedactionOptions? redactionOptions = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExportProfileRecord>> GetExportProfilesAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<string> SaveExportProfileAsync(string workspaceId, ExportProfileUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteExportProfileAsync(string profileId, CancellationToken cancellationToken = default);
}
