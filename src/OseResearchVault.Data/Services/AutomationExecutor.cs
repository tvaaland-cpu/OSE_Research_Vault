using System.Text.Json;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class AutomationExecutor(IAgentService agentService) : IAutomationExecutor
{
    public async Task<AutomationExecutionResult> ExecuteAsync(AutomationRecord automation, CancellationToken cancellationToken = default)
    {
        try
        {
            using var payload = JsonDocument.Parse(automation.PayloadJson);
            var root = payload.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            if (!string.Equals(type, "agent_run_stub", StringComparison.OrdinalIgnoreCase))
            {
                return new AutomationExecutionResult { Success = false, Error = $"Unsupported automation payload type: {type}" };
            }

            if (!root.TryGetProperty("agent_id", out var agentIdElement) || string.IsNullOrWhiteSpace(agentIdElement.GetString()))
            {
                return new AutomationExecutionResult { Success = false, Error = "agent_run_stub requires payload.agent_id" };
            }

            var selectedDocumentIds = root.TryGetProperty("selected_document_ids", out var selectedDocsElement)
                ? selectedDocsElement.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList()
                : new List<string>();

            var runId = await agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = agentIdElement.GetString()!,
                CompanyId = root.TryGetProperty("company_id", out var companyIdElement) ? companyIdElement.GetString() : null,
                Query = root.TryGetProperty("query", out var queryElement) ? queryElement.GetString() : null,
                SelectedDocumentIds = selectedDocumentIds
            }, cancellationToken);

            return new AutomationExecutionResult { Success = true, CreatedRunId = runId };
        }
        catch (Exception ex)
        {
            return new AutomationExecutionResult { Success = false, Error = ex.Message };
        }
    }
}
