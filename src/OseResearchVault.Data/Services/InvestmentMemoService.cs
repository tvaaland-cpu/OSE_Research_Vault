using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.Data.Services;

public sealed class InvestmentMemoService(IAgentService agentService, INoteService noteService) : IInvestmentMemoService
{
    public string BuildPrompt(string companyName)
    {
        var name = string.IsNullOrWhiteSpace(companyName) ? "the selected company" : companyName.Trim();
        return $"""
Generate an investment memo for {name} using only internal vault evidence.

Required sections:
1. Thesis
2. Key Drivers
3. Risks
4. Catalysts (3–12 months)
5. Valuation notes (qualitative unless hard metrics are available)
6. What would change my mind
7. Appendix: citations list

Rules:
- Every factual claim must include one or more citation labels from retrieved context.
- Use concise, decision-grade writing.
- If evidence is missing, explicitly state uncertainty.
""";
    }

    public async Task<InvestmentMemoResult> GenerateInvestmentMemoAsync(string companyId, string companyName, CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(companyName);
        var askResult = await agentService.ExecuteAskMyVaultAsync(new AskMyVaultRequest
        {
            CompanyId = companyId,
            Query = prompt,
            SelectedDocumentIds = []
        }, cancellationToken);

        var artifacts = await agentService.GetArtifactsAsync(askResult.RunId, cancellationToken);
        var memoContent = artifacts.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.Content ?? string.Empty;

        var noteId = await noteService.CreateNoteAsync(new NoteUpsertRequest
        {
            CompanyId = companyId,
            Title = $"Investment Memo - {companyName}",
            Content = memoContent,
            NoteType = askResult.CitationsDetected ? "thesis" : "ai_summary"
        }, cancellationToken);

        return new InvestmentMemoResult
        {
            RunId = askResult.RunId,
            NoteId = noteId,
            CitationsDetected = askResult.CitationsDetected,
            Prompt = prompt,
            MemoContent = memoContent
        };
    }
}
