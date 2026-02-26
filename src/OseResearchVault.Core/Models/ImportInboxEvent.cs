namespace OseResearchVault.Core.Models;

public sealed record ImportInboxEvent(string FileName, bool Succeeded, string? ErrorMessage = null);
