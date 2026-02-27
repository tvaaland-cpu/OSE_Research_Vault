using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Tests;

internal sealed class TestAppSettingsService(string rootDirectory) : IAppSettingsService
{
    private readonly AppSettings _settings = new()
    {
        DatabaseDirectory = Path.Combine(rootDirectory, "db"),
        VaultStorageDirectory = Path.Combine(rootDirectory, "vault")
    };

    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_settings.DatabaseDirectory);
        Directory.CreateDirectory(_settings.VaultStorageDirectory);
        return Task.FromResult(_settings);
    }

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
