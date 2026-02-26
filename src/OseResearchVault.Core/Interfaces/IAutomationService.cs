using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IAutomationService
{
    Task<IReadOnlyList<AutomationRecord>> GetAutomationsAsync(CancellationToken cancellationToken = default);
    Task<string> CreateAutomationAsync(AutomationUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateAutomationAsync(string automationId, AutomationUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteAutomationAsync(string automationId, CancellationToken cancellationToken = default);
    Task SetAutomationEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken = default);
    Task<string> RunNowAsync(string automationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRunRecord>> GetRunsAsync(string automationId, CancellationToken cancellationToken = default);
}
