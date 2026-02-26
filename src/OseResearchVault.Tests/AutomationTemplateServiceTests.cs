using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class AutomationTemplateServiceTests
{
    [Fact]
    public void CreateAutomationFromTemplate_UsesExpectedScheduleAndPayload()
    {
        var service = new InMemoryAutomationTemplateService();

        var daily = service.CreateAutomationFromTemplate("daily-review");
        var weekly = service.CreateAutomationFromTemplate("weekly-watchlist-scan");
        var inbox = service.CreateAutomationFromTemplate("import-inbox-hourly");

        Assert.Equal("Daily review", daily.Name);
        Assert.Equal("Daily at 08:00", daily.ScheduleSummary);
        Assert.Contains("What changed in my vault since yesterday?", daily.Payload);

        Assert.Equal("Weekly watchlist scan", weekly.Name);
        Assert.Equal("Weekly on Monday at 08:30", weekly.ScheduleSummary);
        Assert.Contains("new evidence added in the last 7 days", weekly.Payload);

        Assert.Equal("Import inbox hourly", inbox.Name);
        Assert.Equal("Every 60 minutes", inbox.ScheduleSummary);
        Assert.Contains("\"intervalMinutes\":60", inbox.Payload);
    }
}
