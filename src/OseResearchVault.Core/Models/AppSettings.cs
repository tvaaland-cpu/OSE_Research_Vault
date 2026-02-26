namespace OseResearchVault.Core.Models;

public sealed class AppSettings
{
    public string DatabaseDirectory { get; set; } = string.Empty;
    public string? CurrentWorkspaceId { get; set; }
    public List<WorkspaceSetting> Workspaces { get; set; } = [];
    public string VaultStorageDirectory { get; set; } = string.Empty;
    public string ImportInboxFolderPath { get; set; } = string.Empty;
    public bool ImportInboxEnabled { get; set; }
    public bool MirrorEnabled { get; set; }
    public string MirrorFolderPath { get; set; } = string.Empty;
    public int MirrorFrequencyHours { get; set; } = 24;
    public string? MirrorLastRunAt { get; set; }
    public LlmGenerationSettings DefaultLlmSettings { get; set; } = new();
    public bool FirstRunCompleted { get; set; }

    public string DatabaseFilePath => Path.Combine(DatabaseDirectory, "ose-research-vault.db");
}

public sealed class WorkspaceSetting
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
