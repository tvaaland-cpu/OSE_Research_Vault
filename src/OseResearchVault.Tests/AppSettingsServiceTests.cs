using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_CreatesDefaultFolders()
    {
        var service = new JsonAppSettingsService();

        var settings = await service.GetSettingsAsync();

        Assert.False(string.IsNullOrWhiteSpace(settings.DatabaseDirectory));
        Assert.False(string.IsNullOrWhiteSpace(settings.VaultStorageDirectory));
        Assert.True(Directory.Exists(settings.DatabaseDirectory));
        Assert.True(Directory.Exists(settings.VaultStorageDirectory));
    }
}
