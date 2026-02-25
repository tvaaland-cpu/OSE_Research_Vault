namespace OseResearchVault.Data.Services;

public static class AppPaths
{
    public static string DefaultRootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OSE Research Vault");

    public static string SettingsFilePath => Path.Combine(DefaultRootDirectory, "settings.json");
}
