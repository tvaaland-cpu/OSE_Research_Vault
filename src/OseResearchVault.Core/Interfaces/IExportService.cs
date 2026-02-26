namespace OseResearchVault.Core.Interfaces;

public interface IExportService
{
    Task ExportCompanyResearchPackAsync(string workspaceId, string companyId, string outputFolder, CancellationToken cancellationToken = default);
}
