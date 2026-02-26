using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class InvestmentMemoServiceTests
{
    [Fact]
    public async Task GenerateInvestmentMemoAsync_CreatesThesisNote_FromAskMyVaultArtifacts()
    {
        var agentService = new FakeAgentService();
        var noteService = new FakeNoteService();
        var service = new InvestmentMemoService(agentService, noteService);

        var result = await service.GenerateInvestmentMemoAsync("co-1", "Acme", CancellationToken.None);

        Assert.Equal("run-1", result.RunId);
        Assert.Equal("note-1", result.NoteId);
        Assert.True(result.CitationsDetected);
        Assert.Contains("Thesis", agentService.LastRequest.Query, StringComparison.Ordinal);
        Assert.Equal("thesis", noteService.LastRequest?.NoteType);
        Assert.Equal("co-1", noteService.LastRequest?.CompanyId);
        Assert.Equal("Investment Memo - Acme", noteService.LastRequest?.Title);
        Assert.Equal("memo with [DOC:abc|chunk:1]", noteService.LastRequest?.Content);
    }

    private sealed class FakeAgentService : IAgentService
    {
        public AskMyVaultRequest LastRequest { get; private set; } = new();

        public Task<AskMyVaultResult> ExecuteAskMyVaultAsync(AskMyVaultRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AskMyVaultResult { RunId = "run-1", CitationsDetected = true });
        }

        public Task<IReadOnlyList<AgentArtifactRecord>> GetArtifactsAsync(string runId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentArtifactRecord>>([new() { Id = "a1", RunId = runId, Content = "memo with [DOC:abc|chunk:1]", CreatedAt = "2026-01-01T00:00:00Z" }]);

        public Task<string> CreateAgentAsync(AgentTemplateUpsertRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string> CreateRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AgentTemplateRecord>> GetAgentsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AgentRunRecord>> GetRunsAsync(string? agentId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AgentToolCallRecord>> GetToolCallsAsync(string runId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAgentAsync(string agentId, AgentTemplateUpsertRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateArtifactContentAsync(string artifactId, string content, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeNoteService : INoteService
    {
        public NoteUpsertRequest? LastRequest { get; private set; }

        public Task<string> CreateNoteAsync(NoteUpsertRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult("note-1");
        }

        public Task DeleteNoteAsync(string noteId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<NoteRecord>> GetNotesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string> ImportAiOutputAsync(AiImportRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateNoteAsync(string noteId, NoteUpsertRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
