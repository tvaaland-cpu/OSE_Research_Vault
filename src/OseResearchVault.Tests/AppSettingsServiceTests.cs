using OseResearchVault.Data.Services;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_CreatesDefaultFolders()
    {
        var service = new JsonAppSettingsService();
        AppSettings? settings = null;
        for (var attempt = 0; attempt < 5 && settings is null; attempt++)
        {
            try
            {
                settings = await service.GetSettingsAsync();
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(50);
            }
        }

        var resolved = Assert.IsType<AppSettings>(settings);
        Assert.False(string.IsNullOrWhiteSpace(resolved.DatabaseDirectory));
        Assert.False(string.IsNullOrWhiteSpace(resolved.VaultStorageDirectory));
        Assert.True(Directory.Exists(resolved.DatabaseDirectory));
        Assert.True(Directory.Exists(resolved.VaultStorageDirectory));
    }
}
