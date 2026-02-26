using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class InMemoryAutomationTemplateService : IAutomationTemplateService
{
    private static readonly IReadOnlyList<AutomationTemplateRecord> Templates =
    [
        new()
        {
            Id = "daily-review",
            Name = "Daily review",
            Description = "AskMyVault daily change summary.",
            ScheduleSummary = "Daily at 08:00",
            Payload = "What changed in my vault since yesterday? Summarize by company and list key new documents/notes."
        },
        new()
        {
            Id = "weekly-watchlist-scan",
            Name = "Weekly watchlist scan",
            Description = "AskMyVault scan for each watchlist company.",
            ScheduleSummary = "Weekly on Monday at 08:30",
            Payload = "Summarize thesis, risks, catalysts, and any new evidence added in the last 7 days."
        },
        new()
        {
            Id = "import-inbox-hourly",
            Name = "Import inbox hourly",
            Description = "Retry import inbox on an hourly interval.",
            ScheduleSummary = "Every 60 minutes",
            Payload = "{\"intervalMinutes\":60,\"job\":\"import_inbox\"}"
        }
    ];

    public IReadOnlyList<AutomationTemplateRecord> GetTemplates() => Templates;

    public AutomationRecord CreateAutomationFromTemplate(string templateId)
    {
        var template = Templates.FirstOrDefault(x => string.Equals(x.Id, templateId, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            throw new ArgumentException($"Unknown template '{templateId}'.", nameof(templateId));
        }

        return new AutomationRecord
        {
            Name = template.Name,
            ScheduleSummary = template.ScheduleSummary,
            Payload = template.Payload
        };
    }
}
