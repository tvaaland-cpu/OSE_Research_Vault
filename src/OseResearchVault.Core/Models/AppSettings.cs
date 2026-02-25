namespace OseResearchVault.Core.Models;

public sealed class AppSettings
{
    public string DatabaseDirectory { get; set; } = string.Empty;
    public string VaultStorageDirectory { get; set; } = string.Empty;

    public string DatabaseFilePath => Path.Combine(DatabaseDirectory, "ose-research-vault.db");
}
