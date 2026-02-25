using System.Text.Json;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class JsonAppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.DefaultRootDirectory);

        if (!File.Exists(AppPaths.SettingsFilePath))
        {
            var defaults = BuildDefaults();
            await SaveSettingsAsync(defaults, cancellationToken);
            return defaults;
        }

        await using var stream = File.OpenRead(AppPaths.SettingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
            ?? BuildDefaults();

        EnsureDirectories(settings);
        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        EnsureDirectories(settings);

        await using var stream = File.Create(AppPaths.SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }

    private static AppSettings BuildDefaults()
    {
        var databaseDirectory = Path.Combine(AppPaths.DefaultRootDirectory, "data");
        var vaultDirectory = Path.Combine(AppPaths.DefaultRootDirectory, "vault");

        var settings = new AppSettings
        {
            DatabaseDirectory = databaseDirectory,
            VaultStorageDirectory = vaultDirectory
        };

        EnsureDirectories(settings);
        return settings;
    }

    private static void EnsureDirectories(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DefaultRootDirectory);
        Directory.CreateDirectory(settings.DatabaseDirectory);
        Directory.CreateDirectory(settings.VaultStorageDirectory);
    }
}
