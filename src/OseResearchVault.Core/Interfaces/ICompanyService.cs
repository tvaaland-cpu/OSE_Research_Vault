using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyRecord>> GetCompaniesAsync(CancellationToken cancellationToken = default);
    Task<string> CreateCompanyAsync(CompanyUpsertRequest request, IEnumerable<string> tagIds, CancellationToken cancellationToken = default);
    Task UpdateCompanyAsync(string companyId, CompanyUpsertRequest request, IEnumerable<string> tagIds, CancellationToken cancellationToken = default);
    Task DeleteCompanyAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken = default);
    Task<string> CreateTagAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentRecord>> GetCompanyDocumentsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteRecord>> GetCompanyNotesAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCompanyEventsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompanyMetricRecord>> GetCompanyMetricsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCompanyMetricNamesAsync(string companyId, CancellationToken cancellationToken = default);
    Task UpdateCompanyMetricAsync(string metricId, CompanyMetricUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteCompanyMetricAsync(string metricId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioRecord>> GetCompanyScenariosAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalystRecord>> GetCompanyCatalystsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<string> CreateCatalystAsync(string companyId, CatalystUpsertRequest request, IReadOnlyList<string>? snippetIds = null, CancellationToken cancellationToken = default);
    Task UpdateCatalystAsync(string catalystId, CatalystUpsertRequest request, IReadOnlyList<string>? snippetIds = null, CancellationToken cancellationToken = default);
    Task DeleteCatalystAsync(string catalystId, CancellationToken cancellationToken = default);
    Task<string> CreateScenarioAsync(string companyId, ScenarioUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateScenarioAsync(string scenarioId, ScenarioUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteScenarioAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioKpiRecord>> GetScenarioKpisAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<string> CreateScenarioKpiAsync(string scenarioId, ScenarioKpiUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateScenarioKpiAsync(string scenarioKpiId, ScenarioKpiUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteScenarioKpiAsync(string scenarioKpiId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntryRecord>> GetCompanyJournalEntriesAsync(string companyId, bool reviewDueOnly = false, CancellationToken cancellationToken = default);
    Task<string> CreateJournalEntryAsync(string companyId, JournalEntryUpsertRequest request, IReadOnlyList<string>? tradeIds = null, IReadOnlyList<string>? snippetIds = null, CancellationToken cancellationToken = default);
    Task UpdateJournalEntryAsync(string journalEntryId, JournalEntryUpsertRequest request, IReadOnlyList<string>? tradeIds = null, IReadOnlyList<string>? snippetIds = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCompanyAgentRunsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<PriceImportResult> ImportCompanyDailyPricesCsvAsync(string companyId, string csvFilePath, string? dateColumn = null, string? closeColumn = null, CancellationToken cancellationToken = default);
    Task<PriceDailyRecord?> GetLatestCompanyPriceAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceDailyRecord>> GetCompanyDailyPricesAsync(string companyId, int days = 90, CancellationToken cancellationToken = default);
}
