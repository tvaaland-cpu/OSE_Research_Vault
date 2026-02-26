namespace OseResearchVault.Core.Utilities;

public static class AppEnvironmentPaths
{
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OSE Research Vault");

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");
}
