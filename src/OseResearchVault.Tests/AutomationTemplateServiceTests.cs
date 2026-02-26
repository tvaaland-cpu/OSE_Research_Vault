using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AutomationTemplateServiceTests
{
    [Fact]
    public void CreateAutomationFromTemplate_UsesExpectedScheduleAndPayload()
    {
        var service = new InMemoryAutomationTemplateService();

        var daily = service.CreateAutomationFromTemplate("daily-review");
        var weeklyReview = service.CreateAutomationFromTemplate("weekly-review");
        var watchlistScan = service.CreateAutomationFromTemplate("watchlist-scan");
        var quarterlyReminder = service.CreateAutomationFromTemplate("quarterly-review-reminder");
        var inbox = service.CreateAutomationFromTemplate("import-inbox-hourly");

        Assert.Equal("Daily review", daily.Name);
        Assert.Equal("Daily at 08:00", daily.ScheduleSummary);
        Assert.Contains("What changed in my vault since yesterday?", daily.Payload);

        Assert.Equal("Weekly Review", weeklyReview.Name);
        Assert.Equal("Weekly on Monday at 08:00", weeklyReview.ScheduleSummary);
        Assert.Contains("generate_weekly_review", weeklyReview.Payload);

        Assert.Equal("Watchlist Scan", watchlistScan.Name);
        Assert.Equal("Weekly on Monday at 09:00", watchlistScan.ScheduleSummary);
        Assert.Contains("watchlist_scan", watchlistScan.Payload);

        Assert.Equal("Quarterly Review Reminder", quarterlyReminder.Name);
        Assert.Equal("Quarterly on first business day at 08:00", quarterlyReminder.ScheduleSummary);
        Assert.Contains("quarterly_review_reminder", quarterlyReminder.Payload);
        Assert.Contains("generate_per_company_review", quarterlyReminder.Payload);

        Assert.Equal("Import inbox hourly", inbox.Name);
        Assert.Equal("Every 60 minutes", inbox.ScheduleSummary);
        Assert.Contains("\"intervalMinutes\":60", inbox.Payload);
    }
}
