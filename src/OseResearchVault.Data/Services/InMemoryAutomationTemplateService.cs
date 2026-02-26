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
            Id = "weekly-review",
            Name = "Weekly Review",
            Description = "Generate the weekly review note every Monday morning.",
            ScheduleSummary = "Weekly on Monday at 08:00",
            Payload = "{\"job\":\"generate_weekly_review\"}"
        },
        new()
        {
            Id = "watchlist-scan",
            Name = "Watchlist Scan",
            Description = "Compile watchlist changes and key catalysts each week.",
            ScheduleSummary = "Weekly on Monday at 09:00",
            Payload = "{\"job\":\"watchlist_scan\"}"
        },
        new()
        {
            Id = "quarterly-review-reminder",
            Name = "Quarterly Review Reminder",
            Description = "Notify on the first business day of each quarter to generate per-company quarterly reviews.",
            ScheduleSummary = "Quarterly on first business day at 08:00",
            Payload = "{\"job\":\"quarterly_review_reminder\",\"action\":\"generate_per_company_review\"}"
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
