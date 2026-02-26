namespace OseResearchVault.Core.Models;

public sealed class ContextPack
{
    public IReadOnlyList<ContextItem> Items { get; init; } = Array.Empty<ContextItem>();
}

public sealed class ContextItem
{
    public string CitationLabel { get; init; } = string.Empty;
    public string SourceDescription { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class AskVaultStyleOptions
{
    public bool PreferBulletedAnswer { get; init; } = true;
    public bool IncludeGapsSection { get; init; } = true;
}
