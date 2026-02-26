namespace OseResearchVault.Core.Models;

public sealed class RedactionOptions
{
    public bool MaskEmails { get; set; } = true;
    public bool MaskPhones { get; set; } = true;
    public bool MaskPaths { get; set; } = true;
    public bool MaskSecrets { get; set; } = true;
    public bool ExcludePrivateTaggedItems { get; set; } = true;
}

public sealed class RedactionHit
{
    public required string Category { get; init; }
    public required string Value { get; init; }
    public required string Replacement { get; init; }
}

public sealed class RedactionResult
{
    public required string RedactedText { get; init; }
    public required IReadOnlyList<RedactionHit> Hits { get; init; }
}

public sealed class ExportProfileRecord
{
    public string ProfileId { get; init; } = string.Empty;
    public string WorkspaceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public RedactionOptions Options { get; init; } = new();
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed class ExportProfileUpsertRequest
{
    public string? ProfileId { get; init; }
    public string Name { get; init; } = string.Empty;
    public RedactionOptions Options { get; init; } = new();
}
