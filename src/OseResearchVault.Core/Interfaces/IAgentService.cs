using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IAgentService
{
    Task<IReadOnlyList<AgentTemplateRecord>> GetAgentsAsync(CancellationToken cancellationToken = default);
    Task<string> CreateAgentAsync(AgentTemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateAgentAsync(string agentId, AgentTemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentRunRecord>> GetRunsAsync(string? agentId = null, CancellationToken cancellationToken = default);
    Task<string> CreateRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentToolCallRecord>> GetToolCallsAsync(string runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentArtifactRecord>> GetArtifactsAsync(string runId, CancellationToken cancellationToken = default);
    Task UpdateArtifactContentAsync(string artifactId, string content, CancellationToken cancellationToken = default);
}
