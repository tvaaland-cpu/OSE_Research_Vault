using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IAppSettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
