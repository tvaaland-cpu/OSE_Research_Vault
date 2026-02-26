using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IAutomationExecutor
{
    Task<AutomationExecutionResult> ExecuteAsync(AutomationRecord automation, CancellationToken cancellationToken = default);
}
