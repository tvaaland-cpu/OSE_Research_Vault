namespace OseResearchVault.Core.Interfaces;

public interface IDiagnosticsService
{
    Task ExportAsync(string zipFilePath, bool includeMigrationList = true, CancellationToken cancellationToken = default);
}
