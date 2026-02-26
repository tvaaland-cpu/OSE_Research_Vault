using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IAutomationRepository
{
    Task<string> CreateAutomationAsync(AutomationRecord automation, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRecord>> GetEnabledAutomationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRecord>> GetDueAutomationsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task UpdateScheduleAsync(string automationId, string? lastRunAt, string nextRunAt, CancellationToken cancellationToken = default);
    Task<string> CreateAutomationRunAsync(string automationId, string startedAt, CancellationToken cancellationToken = default);
    Task CompleteAutomationRunAsync(string automationRunId, string status, string endedAt, string? error, string? createdRunId, CancellationToken cancellationToken = default);
}
