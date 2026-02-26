using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using OseResearchVault.App.Services;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IDocumentImportService _documentImportService;
    private readonly ICompanyService _companyService;
    private readonly INoteService _noteService;
    private readonly IEvidenceService _evidenceService;
    private readonly ISearchService _searchService;
    private readonly IAgentService _agentService;
    private readonly IDataQualityService _dataQualityService;
    private readonly IAutomationTemplateService _automationTemplateService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IImportInboxWatcher _importInboxWatcher;
    private readonly INotificationService _notificationService;
    private readonly IAutomationService _automationService;
    private readonly IAskMyVaultService _askMyVaultService;
    private readonly IMetricService _metricService;
    private readonly IMetricConflictDialogService _metricConflictDialogService;
    private readonly IUserDialogService _dialogService;
    private readonly IConnectorRegistry _connectorRegistry;
    private readonly IConnectorHttpClient _connectorHttpClient;
    private readonly IMetricService _metricService;
    private NavigationItem _selectedItem;
    private DocumentListItemViewModel? _selectedDocument;
    private CompanyListItemViewModel? _selectedCompany;
    private NoteListItemViewModel? _selectedNote;
    private CompanyOptionViewModel? _selectedDocumentCompany;
    private CompanyOptionViewModel? _selectedNoteCompany;
    private CompanyOptionViewModel? _selectedHubCompany;
    private string _documentStatusMessage = "Drop files to import into the vault.";
    private string _detailsSummary = "Select a document to view metadata.";
    private string _detailsTextPreview = string.Empty;
    private string _companyName = string.Empty;
    private string _companyTicker = string.Empty;
    private string _companyIsin = string.Empty;
    private string _companySector = string.Empty;
    private string _companyIndustry = string.Empty;
    private string _companyCurrency = string.Empty;
    private string _companySummary = string.Empty;
    private string _newTagName = string.Empty;
    private string _companyStatusMessage = "Create and manage companies.";
    private string _noteTitle = string.Empty;
    private string _noteContent = string.Empty;
    private string _selectedNoteType = "manual";
    private CompanyOptionViewModel? _notesFilterCompany;
    private string _notesFilterType = "All";
    private string _noteStatusMessage = "Create and manage notes.";
    private string _searchQuery = string.Empty;
    private WorkspaceOptionViewModel? _selectedSearchWorkspace;
    private CompanyOptionViewModel? _selectedSearchCompany;
    private string _selectedSearchType = "All";
    private DateTime? _searchDateFrom;
    private DateTime? _searchDateTo;
    private SearchResultListItemViewModel? _selectedSearchResult;
    private string _searchStatusMessage = "Search notes, documents, snippets, and artifacts.";
    private string _askVaultQuery = string.Empty;
    private CompanyOptionViewModel? _selectedAskVaultCompany;
    private AskVaultContextItemViewModel? _selectedAskVaultContextItem;
    private string _askVaultPrompt = string.Empty;
    private string _askVaultStatusMessage = "Preview retrieval context and prompt before running an LLM.";
    private AgentTemplateListItemViewModel? _selectedAgentTemplate;
    private AgentRunListItemViewModel? _selectedAgentRun;
    private ArtifactListItemViewModel? _selectedRunArtifact;
    private CompanyOptionViewModel? _selectedRunCompany;
    private string _agentName = string.Empty;
    private string _agentGoal = string.Empty;
    private string _agentInstructions = string.Empty;
    private string _agentAllowedTools = "[]";
    private string _agentOutputSchema = string.Empty;
    private string _agentEvidencePolicy = string.Empty;
    private string _agentQuery = string.Empty;
    private string _agentStatusMessage = "Create reusable agent templates and run history.";
    private string _runInputSummary = "Select a run to view notebook details.";
    private string _runToolCallsSummary = "(empty for MVP)";
    private string _metricName = string.Empty;
    private string _metricPeriod = string.Empty;
    private string _metricValue = string.Empty;
    private string _metricUnit = string.Empty;
    private string _metricCurrency = string.Empty;
    private string _metricStatusMessage = "Track company metrics.";
    private string _latestClosePrice = "-";
    private string _latestCloseDate = "-";
    private string _selectedDocumentWorkspaceId = string.Empty;
    private string _dataQualityStatusMessage = "Run a report to detect vault data quality issues.";
    private DataQualityUnlinkedListItemViewModel? _selectedUnlinkedDocument;
    private DataQualityUnlinkedListItemViewModel? _selectedUnlinkedNote;
    private CompanyOptionViewModel? _selectedQualityCompany;
    private DataQualityDuplicateGroupViewModel? _selectedDuplicateGroup;
    private DataQualityDuplicateDocumentViewModel? _selectedDuplicateKeepDocument;
    private DataQualityArtifactGapViewModel? _selectedEvidenceGap;
    private DataQualityEnrichmentSuggestionViewModel? _selectedEnrichmentSuggestion;
    private readonly ITradeService _tradeService;
    private readonly IPositionAnalyticsService _positionAnalyticsService;
    private string _dashboardStatusMessage = "Portfolio dashboard ready.";
    private string _dashboardPositionFilter = "Open";
    private string _dashboardCurrencyFilter = "All";
    private string _dashboardTotalInvested = "0.00";
    private string _dashboardTotalMarketValue = "N/A";
    private string _dashboardTotalUnrealizedPnl = "N/A";
    private string _dashboardTotalRealizedPnl = "0.00";
    private string _dashboardBiggestWinner = "N/A";
    private string _dashboardBiggestLoser = "N/A";

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IDataQualityService dataQualityService)
    private string _automationStatusMessage = "Create automations from built-in templates.";

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IAutomationTemplateService automationTemplateService)
    private string _importInboxFolderPath = string.Empty;
    private bool _importInboxEnabled;

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IAppSettingsService appSettingsService, IImportInboxWatcher importInboxWatcher)
    private NotificationListItemViewModel? _selectedNotification;
    private string _toastMessage = string.Empty;
    private bool _isToastVisible;
    private CancellationTokenSource? _toastCts;

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, INotificationService notificationService)
    private AutomationListItemViewModel? _selectedAutomation;
    private AutomationRunListItemViewModel? _selectedAutomationRun;
    private string _automationStatusMessage = "Create and schedule automations.";

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IAutomationService automationService)
    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IAskMyVaultService askMyVaultService)
    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IMetricService metricService, IMetricConflictDialogService metricConflictDialogService)
    private string _evidenceCoverageLabel = "0 evidence links";
    private string _artifactEvidenceStatusMessage = "Select an artifact to view linked evidence.";
    private string _investmentMemoStatusMessage = string.Empty;
    private string _selectedDocumentWorkspaceId = string.Empty;
    private CompanyMetricListItemViewModel? _selectedHubMetric;
    private ScenarioListItemViewModel? _selectedScenario;
    private ScenarioKpiListItemViewModel? _selectedScenarioKpi;
    private string _selectedMetricNameFilter = "All";
    private string _metricPeriodFilter = string.Empty;
    private string _metricEditName = string.Empty;
    private string _metricEditPeriod = string.Empty;
    private string _metricEditValue = string.Empty;
    private string _metricEditUnit = string.Empty;
    private string _metricEditCurrency = string.Empty;
    private string _scenarioName = "Base";
    private string _scenarioProbability = "0.5";
    private string _scenarioAssumptions = string.Empty;
    private string _scenarioKpiName = string.Empty;
    private string _scenarioKpiPeriod = string.Empty;
    private string _scenarioKpiValue = string.Empty;
    private string _scenarioKpiUnit = string.Empty;
    private string _scenarioKpiCurrency = string.Empty;
    private string _scenarioKpiSnippetId = string.Empty;
    private string _scenarioStatusMessage = "Model scenarios per company.";
    private ConnectorListItemViewModel? _selectedConnector;
    private string _connectorUrl = "https://example.com";
    private string _connectorRunResult = "Run a connector to ingest snapshots.";

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IUserDialogService dialogService)

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, IMetricService metricService)
    {
        _documentImportService = documentImportService;
        _companyService = companyService;
        _noteService = noteService;
        _evidenceService = evidenceService;
        _searchService = searchService;
        _agentService = agentService;
        _dataQualityService = dataQualityService;
        _automationTemplateService = automationTemplateService;
        _appSettingsService = appSettingsService;
        _importInboxWatcher = importInboxWatcher;
        _notificationService = notificationService;
        _automationService = automationService;
        _askMyVaultService = askMyVaultService;
        _metricService = metricService;
        _metricConflictDialogService = metricConflictDialogService;
        _dialogService = dialogService;
        _metricService = metricService;
        _tradeService = tradeService;
        _positionAnalyticsService = positionAnalyticsService;

        NavigationItems =
        [
            new NavigationItem("Dashboard"),
            new NavigationItem("Companies"),
            new NavigationItem("Company Hub"),
            new NavigationItem("Watchlist"),
            new NavigationItem("Documents"),
            new NavigationItem("Notes"),
            new NavigationItem("Agents"),
            new NavigationItem("Automations"),
            new NavigationItem("Ask My Vault"),
            new NavigationItem("Search"),
            new NavigationItem("Data Quality"),
            new NavigationItem("Inbox"),
            new NavigationItem("Settings"),
            new NavigationItem("Connectors")
        ];

        Documents = [];
        Companies = [];
        Notes = [];
        AllNotes = [];
        CompanyOptions = [];
        NoteFilterCompanies = [];
        AvailableTags = [];
        NoteTypes = ["manual", "meeting", "ai_summary", "thesis", "risk", "catalyst", "log"];
        NoteFilterTypes = ["All", .. NoteTypes];
        HubDocuments = [];
        HubNotes = [];
        HubEvents = [];
        HubMetrics = [];
        Scenarios = [];
        ScenarioKpis = [];
        AllHubMetrics = [];
        HubPrices = [];
        MetricNameOptions = ["All"];
        HubAgentRuns = [];
        SearchResults = [];
        AskVaultContextItems = [];
        WorkspaceOptions = [];
        AgentTemplates = [];
        AgentRuns = [];
        RunSelectableDocuments = [];
        RunArtifacts = [];
        AutomationTemplates = [];
        Automations = [];
        ArtifactEvidenceLinks = [];
        DocumentSnippets = [];
        Notifications = [];
        Connectors = [];
        Automations = [];
        AutomationRuns = [];
        SearchTypeOptions = ["All", "Notes", "Documents", "Snippets", "Artifacts"];
        DataQualityDuplicateGroups = [];
        DataQualityUnlinkedDocuments = [];
        DataQualityUnlinkedNotes = [];
        DataQualityEvidenceGaps = [];
        DataQualityMetricIssues = [];
        DataQualitySnippetIssues = [];
        DataQualityEnrichmentSuggestions = [];
        DashboardRows = [];
        DashboardCurrencyOptions = ["All"];

        RefreshDocumentsCommand = new RelayCommand(() => _ = LoadDocumentsAsync());
        SaveDocumentCompanyCommand = new RelayCommand(() => _ = SaveSelectedDocumentCompanyAsync(), () => SelectedDocument is not null);
        SaveCompanyCommand = new RelayCommand(() => _ = SaveCompanyAsync(), () => !string.IsNullOrWhiteSpace(CompanyName));
        DeleteCompanyCommand = new RelayCommand(() => _ = DeleteCompanyAsync(), () => SelectedCompany is not null);
        AddTagCommand = new RelayCommand(() => _ = AddTagAsync(), () => !string.IsNullOrWhiteSpace(NewTagName));
        NewCompanyCommand = new RelayCommand(ClearCompanyForm);
        SaveNoteCommand = new RelayCommand(() => _ = SaveNoteAsync(), () => !string.IsNullOrWhiteSpace(NoteTitle));
        DeleteNoteCommand = new RelayCommand(() => _ = DeleteNoteAsync(), () => SelectedNote is not null);
        NewNoteCommand = new RelayCommand(ClearNoteForm);
        ExecuteSearchCommand = new RelayCommand(() => _ = ExecuteSearchAsync(), () => !string.IsNullOrWhiteSpace(SearchQuery));
        OpenSearchResultCommand = new RelayCommand(() => OpenSearchResult(SelectedSearchResult), () => SelectedSearchResult is not null);
        RetrieveAskVaultCommand = new RelayCommand(() => _ = RetrieveAskVaultPreviewAsync(), () => !string.IsNullOrWhiteSpace(AskVaultQuery));
        OpenAskVaultContextItemCommand = new RelayCommand(() => OpenAskVaultContextItem(SelectedAskVaultContextItem), () => SelectedAskVaultContextItem is not null);
        SaveAgentTemplateCommand = new RelayCommand(() => _ = SaveAgentTemplateAsync(), () => !string.IsNullOrWhiteSpace(AgentName));
        NewAgentTemplateCommand = new RelayCommand(ClearAgentTemplateForm);
        RunAgentCommand = new RelayCommand(() => _ = RunAgentAsync());
        RunAgentCommand = new RelayCommand(() => _ = RunAgentAsync(), () => !string.IsNullOrWhiteSpace(AgentQuery));
        SaveArtifactCommand = new RelayCommand(() => _ = SaveArtifactAsync(), () => SelectedRunArtifact is not null);
        RefreshDataQualityCommand = new RelayCommand(() => _ = LoadDataQualityReportAsync());
        LinkSelectedUnlinkedDocumentCommand = new RelayCommand(() => _ = LinkSelectedUnlinkedDocumentAsync(), () => SelectedUnlinkedDocument is not null && SelectedQualityCompany is not null);
        LinkSelectedUnlinkedNoteCommand = new RelayCommand(() => _ = LinkSelectedUnlinkedNoteAsync(), () => SelectedUnlinkedNote is not null && SelectedQualityCompany is not null);
        ArchiveDuplicateGroupCommand = new RelayCommand(() => _ = ArchiveDuplicateGroupAsync(), () => SelectedDuplicateGroup is not null && SelectedDuplicateKeepDocument is not null);
        OpenEvidenceGapCommand = new RelayCommand(OpenSelectedEvidenceGap, () => SelectedEvidenceGap is not null);
        ApplySelectedEnrichmentSuggestionCommand = new RelayCommand(() => _ = ApplySelectedEnrichmentSuggestionAsync(), () => SelectedEnrichmentSuggestion is not null);
        CreateDailyReviewAutomationCommand = new RelayCommand(() => CreateAutomationFromTemplate("daily-review"));
        CreateWeeklyWatchlistAutomationCommand = new RelayCommand(() => CreateAutomationFromTemplate("weekly-review"));
        CreateQuarterlyReviewReminderAutomationCommand = new RelayCommand(() => CreateAutomationFromTemplate("quarterly-review-reminder"));
        CreateImportInboxAutomationCommand = new RelayCommand(() => CreateAutomationFromTemplate("import-inbox-hourly"));
        SaveImportInboxSettingsCommand = new RelayCommand(() => _ = SaveImportInboxSettingsAsync());

        _importInboxWatcher.FileImported += OnImportInboxFileImported;
        RefreshInboxCommand = new RelayCommand(() => _ = LoadNotificationsAsync());
        MarkNotificationReadCommand = new RelayCommand(() => _ = MarkNotificationReadAsync(), () => SelectedNotification is not null && !SelectedNotification.IsRead);
        NewAutomationCommand = new RelayCommand(() => _ = NewAutomationAsync());
        EditAutomationCommand = new RelayCommand(() => _ = EditAutomationAsync(), () => SelectedAutomation is not null);
        DeleteAutomationCommand = new RelayCommand(() => _ = DeleteAutomationAsync(), () => SelectedAutomation is not null);
        RunAutomationNowCommand = new RelayCommand(() => _ = RunAutomationNowAsync(), () => SelectedAutomation is not null);
        ToggleAutomationEnabledCommand = new RelayCommand(() => _ = ToggleAutomationEnabledAsync(), () => SelectedAutomation is not null);
        SaveMetricCommand = new RelayCommand(() => _ = SaveMetricAsync(), () => SelectedHubCompany is not null && !string.IsNullOrWhiteSpace(MetricName) && !string.IsNullOrWhiteSpace(MetricPeriod));
        OpenMetricEvidenceCommand = new RelayCommand(param => _ = OpenMetricEvidenceAsync(param as CompanyMetricListItemViewModel));
        SaveMetricCommand = new RelayCommand(() => _ = SaveSelectedMetricAsync(), () => SelectedHubMetric is not null && !string.IsNullOrWhiteSpace(MetricEditName));
        DeleteMetricCommand = new RelayCommand(() => _ = DeleteSelectedMetricAsync(), () => SelectedHubMetric is not null);
        SaveScenarioCommand = new RelayCommand(() => _ = SaveScenarioAsync(), () => SelectedHubCompany is not null && !string.IsNullOrWhiteSpace(ScenarioName));
        DeleteScenarioCommand = new RelayCommand(() => _ = DeleteScenarioAsync(), () => SelectedScenario is not null);
        SaveScenarioKpiCommand = new RelayCommand(() => _ = SaveScenarioKpiAsync(), () => SelectedScenario is not null && !string.IsNullOrWhiteSpace(ScenarioKpiName) && !string.IsNullOrWhiteSpace(ScenarioKpiPeriod));
        DeleteScenarioKpiCommand = new RelayCommand(() => _ = DeleteScenarioKpiAsync(), () => SelectedScenarioKpi is not null);
        RunConnectorCommand = new RelayCommand(() => _ = RunSelectedConnectorAsync(), () => SelectedConnector is not null);

        _selectedItem = NavigationItems[1];
        _ = InitializeAsync();
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public ObservableCollection<DocumentListItemViewModel> Documents { get; }
    public ObservableCollection<CompanyListItemViewModel> Companies { get; }
    public ObservableCollection<NoteListItemViewModel> Notes { get; }
    public ObservableCollection<NoteListItemViewModel> AllNotes { get; }
    public ObservableCollection<CompanyOptionViewModel> CompanyOptions { get; }
    public ObservableCollection<CompanyOptionViewModel> NoteFilterCompanies { get; }
    public ObservableCollection<TagSelectionViewModel> AvailableTags { get; }
    public ObservableCollection<string> HubDocuments { get; }
    public ObservableCollection<string> HubNotes { get; }
    public ObservableCollection<string> HubEvents { get; }
    public ObservableCollection<CompanyMetricListItemViewModel> HubMetrics { get; }
    public ObservableCollection<ScenarioListItemViewModel> Scenarios { get; }
    public ObservableCollection<ScenarioKpiListItemViewModel> ScenarioKpis { get; }
    public ObservableCollection<CompanyMetricListItemViewModel> AllHubMetrics { get; }
    public ObservableCollection<PriceDailyListItemViewModel> HubPrices { get; }
    public ObservableCollection<string> MetricNameOptions { get; }
    public ObservableCollection<string> HubAgentRuns { get; }
    public ObservableCollection<SearchResultListItemViewModel> SearchResults { get; }
    public ObservableCollection<AskVaultContextItemViewModel> AskVaultContextItems { get; }
    public ObservableCollection<WorkspaceOptionViewModel> WorkspaceOptions { get; }
    public ObservableCollection<AgentTemplateListItemViewModel> AgentTemplates { get; }
    public ObservableCollection<AgentRunListItemViewModel> AgentRuns { get; }
    public ObservableCollection<DocumentListItemViewModel> RunSelectableDocuments { get; }
    public ObservableCollection<ArtifactListItemViewModel> RunArtifacts { get; }
    public ObservableCollection<AutomationTemplateListItemViewModel> AutomationTemplates { get; }
    public ObservableCollection<AutomationListItemViewModel> Automations { get; }
    public ObservableCollection<ArtifactEvidenceLinkListItemViewModel> ArtifactEvidenceLinks { get; }
    public ObservableCollection<ArtifactEvidenceListItemViewModel> ArtifactEvidenceLinks { get; }
    public ObservableCollection<DocumentSnippetListItemViewModel> DocumentSnippets { get; }
    public ObservableCollection<DataQualityDuplicateGroupViewModel> DataQualityDuplicateGroups { get; }
    public ObservableCollection<DataQualityUnlinkedListItemViewModel> DataQualityUnlinkedDocuments { get; }
    public ObservableCollection<DataQualityUnlinkedListItemViewModel> DataQualityUnlinkedNotes { get; }
    public ObservableCollection<DataQualityArtifactGapViewModel> DataQualityEvidenceGaps { get; }
    public ObservableCollection<DataQualityMetricIssueViewModel> DataQualityMetricIssues { get; }
    public ObservableCollection<DataQualitySnippetIssueViewModel> DataQualitySnippetIssues { get; }
    public ObservableCollection<DataQualityEnrichmentSuggestionViewModel> DataQualityEnrichmentSuggestions { get; }
    public ObservableCollection<NotificationListItemViewModel> Notifications { get; }
    public ObservableCollection<ConnectorListItemViewModel> Connectors { get; }
    public ObservableCollection<AutomationListItemViewModel> Automations { get; }
    public ObservableCollection<AutomationRunListItemViewModel> AutomationRuns { get; }
    public IReadOnlyList<string> NoteTypes { get; }
    public IReadOnlyList<string> NoteFilterTypes { get; }
    public IReadOnlyList<string> SearchTypeOptions { get; }
    public RelayCommand RefreshDocumentsCommand { get; }
    public RelayCommand SaveDocumentCompanyCommand { get; }
    public RelayCommand SaveCompanyCommand { get; }
    public RelayCommand DeleteCompanyCommand { get; }
    public RelayCommand AddTagCommand { get; }
    public RelayCommand NewCompanyCommand { get; }
    public RelayCommand SaveNoteCommand { get; }
    public RelayCommand DeleteNoteCommand { get; }
    public RelayCommand NewNoteCommand { get; }
    public RelayCommand ExecuteSearchCommand { get; }
    public RelayCommand OpenSearchResultCommand { get; }
    public RelayCommand RetrieveAskVaultCommand { get; }
    public RelayCommand OpenAskVaultContextItemCommand { get; }
    public RelayCommand SaveAgentTemplateCommand { get; }
    public RelayCommand NewAgentTemplateCommand { get; }
    public RelayCommand RunAgentCommand { get; }
    public RelayCommand SaveArtifactCommand { get; }
    public RelayCommand RefreshDataQualityCommand { get; }
    public RelayCommand LinkSelectedUnlinkedDocumentCommand { get; }
    public RelayCommand LinkSelectedUnlinkedNoteCommand { get; }
    public RelayCommand ArchiveDuplicateGroupCommand { get; }
    public RelayCommand OpenEvidenceGapCommand { get; }
    public RelayCommand ApplySelectedEnrichmentSuggestionCommand { get; }
    public RelayCommand CreateDailyReviewAutomationCommand { get; }
    public RelayCommand CreateWeeklyWatchlistAutomationCommand { get; }
    public RelayCommand CreateQuarterlyReviewReminderAutomationCommand { get; }
    public RelayCommand CreateImportInboxAutomationCommand { get; }
    public RelayCommand SaveImportInboxSettingsCommand { get; }
    public RelayCommand RefreshInboxCommand { get; }
    public RelayCommand MarkNotificationReadCommand { get; }
    public RelayCommand NewAutomationCommand { get; }
    public RelayCommand EditAutomationCommand { get; }
    public RelayCommand DeleteAutomationCommand { get; }
    public RelayCommand RunAutomationNowCommand { get; }
    public RelayCommand ToggleAutomationEnabledCommand { get; }
    public RelayCommand SaveMetricCommand { get; }
    public RelayCommand OpenMetricEvidenceCommand { get; }
    public RelayCommand SaveMetricCommand { get; }
    public RelayCommand DeleteMetricCommand { get; }
    public RelayCommand SaveScenarioCommand { get; }
    public RelayCommand DeleteScenarioCommand { get; }
    public RelayCommand SaveScenarioKpiCommand { get; }
    public RelayCommand DeleteScenarioKpiCommand { get; }
    public RelayCommand RunConnectorCommand { get; }

    public NavigationItem SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(IsDocumentsSelected));
                OnPropertyChanged(nameof(IsCompaniesSelected));
                OnPropertyChanged(nameof(IsNotesSelected));
                OnPropertyChanged(nameof(IsCompanyHubSelected));
                OnPropertyChanged(nameof(IsSearchSelected));
                OnPropertyChanged(nameof(IsAgentsSelected));
                OnPropertyChanged(nameof(IsDataQualitySelected));
                OnPropertyChanged(nameof(IsAutomationsSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                OnPropertyChanged(nameof(IsInboxSelected));
                OnPropertyChanged(nameof(IsAutomationsSelected));
                OnPropertyChanged(nameof(IsAskMyVaultSelected));
                OnPropertyChanged(nameof(IsConnectorsSelected));
                OnPropertyChanged(nameof(IsDashboardSelected));
            }
        }
    }

    public bool IsDashboardSelected => IsSelected("Dashboard");
    public bool IsDocumentsSelected => IsSelected("Documents");
    public bool IsCompaniesSelected => IsSelected("Companies");
    public bool IsNotesSelected => IsSelected("Notes");
    public bool IsCompanyHubSelected => IsSelected("Company Hub");
    public bool IsSearchSelected => IsSelected("Search");
    public bool IsAgentsSelected => IsSelected("Agents");
    public bool IsDataQualitySelected => IsSelected("Data Quality");
    public bool IsAutomationsSelected => IsSelected("Automations");

    public string InvestmentMemoStatusMessage
    {
        get => _investmentMemoStatusMessage;
        set => SetProperty(ref _investmentMemoStatusMessage, value);
    }

    public bool IsSettingsSelected => IsSelected("Settings");
    public bool IsInboxSelected => IsSelected("Inbox");
    public bool IsAutomationsSelected => IsSelected("Automations");

    public AutomationListItemViewModel? SelectedAutomation
    {
        get => _selectedAutomation;
        set
        {
            if (SetProperty(ref _selectedAutomation, value))
            {
                EditAutomationCommand.RaiseCanExecuteChanged();
                DeleteAutomationCommand.RaiseCanExecuteChanged();
                RunAutomationNowCommand.RaiseCanExecuteChanged();
                ToggleAutomationEnabledCommand.RaiseCanExecuteChanged();
                _ = LoadAutomationRunsAsync(value?.Id);
            }
        }
    }

    public AutomationRunListItemViewModel? SelectedAutomationRun
    {
        get => _selectedAutomationRun;
        set => SetProperty(ref _selectedAutomationRun, value);
    }

    public string AutomationStatusMessage
    {
        get => _automationStatusMessage;
        set => SetProperty(ref _automationStatusMessage, value);
    }
    public bool IsAskMyVaultSelected => IsSelected("Ask My Vault");
    public bool IsConnectorsSelected => IsSelected("Connectors");

    public ObservableCollection<PortfolioAllocationRowViewModel> DashboardRows { get; }
    public ObservableCollection<string> DashboardCurrencyOptions { get; }
    public string DashboardStatusMessage { get => _dashboardStatusMessage; set => SetProperty(ref _dashboardStatusMessage, value); }
    public string DashboardPositionFilter { get => _dashboardPositionFilter; set { if (SetProperty(ref _dashboardPositionFilter, value)) { ApplyDashboardFilters(); } } }
    public string DashboardCurrencyFilter { get => _dashboardCurrencyFilter; set { if (SetProperty(ref _dashboardCurrencyFilter, value)) { ApplyDashboardFilters(); } } }
    public string DashboardTotalInvested { get => _dashboardTotalInvested; set => SetProperty(ref _dashboardTotalInvested, value); }
    public string DashboardTotalMarketValue { get => _dashboardTotalMarketValue; set => SetProperty(ref _dashboardTotalMarketValue, value); }
    public string DashboardTotalUnrealizedPnl { get => _dashboardTotalUnrealizedPnl; set => SetProperty(ref _dashboardTotalUnrealizedPnl, value); }
    public string DashboardTotalRealizedPnl { get => _dashboardTotalRealizedPnl; set => SetProperty(ref _dashboardTotalRealizedPnl, value); }
    public string DashboardBiggestWinner { get => _dashboardBiggestWinner; set => SetProperty(ref _dashboardBiggestWinner, value); }
    public string DashboardBiggestLoser { get => _dashboardBiggestLoser; set => SetProperty(ref _dashboardBiggestLoser, value); }

    public ConnectorListItemViewModel? SelectedConnector
    {
        get => _selectedConnector;
        set
        {
            if (SetProperty(ref _selectedConnector, value))
            {
                RunConnectorCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ConnectorUrl
    {
        get => _connectorUrl;
        set => SetProperty(ref _connectorUrl, value);
    }

    public string ConnectorRunResult
    {
        get => _connectorRunResult;
        set => SetProperty(ref _connectorRunResult, value);
    }

    public DocumentListItemViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                SaveDocumentCompanyCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanCreateSnippet));
                SelectedDocumentCompany = value is null
                    ? null
                    : CompanyOptions.FirstOrDefault(c => c.Id == value.CompanyId);
                _ = LoadDocumentDetailsAsync(value?.Id);
            }
        }
    }

    public CompanyListItemViewModel? SelectedCompany
    {
        get => _selectedCompany;
        set
        {
            if (SetProperty(ref _selectedCompany, value))
            {
                DeleteCompanyCommand.RaiseCanExecuteChanged();
                PopulateCompanyForm(value);
            }
        }
    }

    public NoteListItemViewModel? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (SetProperty(ref _selectedNote, value))
            {
                DeleteNoteCommand.RaiseCanExecuteChanged();
                PopulateNoteForm(value);
            }
        }
    }

    public CompanyOptionViewModel? SelectedDocumentCompany
    {
        get => _selectedDocumentCompany;
        set => SetProperty(ref _selectedDocumentCompany, value);
    }

    public CompanyOptionViewModel? SelectedNoteCompany
    {
        get => _selectedNoteCompany;
        set => SetProperty(ref _selectedNoteCompany, value);
    }

    public CompanyOptionViewModel? SelectedHubCompany
    {
        get => _selectedHubCompany;
        set
        {
            if (SetProperty(ref _selectedHubCompany, value))
            {
                SaveMetricCommand.RaiseCanExecuteChanged();
                SaveScenarioCommand.RaiseCanExecuteChanged();
                _ = LoadCompanyHubAsync(value?.Id);
            }
        }
    }

    public string MetricName
    {
        get => _metricName;
        set
        {
            if (SetProperty(ref _metricName, value))
            {
                SaveMetricCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string MetricPeriod
    {
        get => _metricPeriod;
        set
        {
            if (SetProperty(ref _metricPeriod, value))
            {
                SaveMetricCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string MetricValue { get => _metricValue; set => SetProperty(ref _metricValue, value); }
    public string MetricUnit { get => _metricUnit; set => SetProperty(ref _metricUnit, value); }
    public string MetricCurrency { get => _metricCurrency; set => SetProperty(ref _metricCurrency, value); }
    public string MetricStatusMessage { get => _metricStatusMessage; set => SetProperty(ref _metricStatusMessage, value); }
    public string LatestClosePrice { get => _latestClosePrice; set => SetProperty(ref _latestClosePrice, value); }
    public string LatestCloseDate { get => _latestCloseDate; set => SetProperty(ref _latestCloseDate, value); }
    public CompanyMetricListItemViewModel? SelectedHubMetric
    {
        get => _selectedHubMetric;
        set
        {
            if (SetProperty(ref _selectedHubMetric, value))
            {
                SaveMetricCommand.RaiseCanExecuteChanged();
                DeleteMetricCommand.RaiseCanExecuteChanged();

                MetricEditName = value?.MetricName ?? string.Empty;
                MetricEditPeriod = value?.Period ?? string.Empty;
                MetricEditValue = value?.ValueDisplay ?? string.Empty;
                MetricEditUnit = value?.Unit ?? string.Empty;
                MetricEditCurrency = value?.Currency ?? string.Empty;
            }
        }
    }

    public ScenarioListItemViewModel? SelectedScenario
    {
        get => _selectedScenario;
        set
        {
            if (SetProperty(ref _selectedScenario, value))
            {
                DeleteScenarioCommand.RaiseCanExecuteChanged();
                SaveScenarioKpiCommand.RaiseCanExecuteChanged();
                ScenarioName = value?.Name ?? "Base";
                ScenarioProbability = value is null ? "0.5" : value.Probability.ToString(CultureInfo.InvariantCulture);
                ScenarioAssumptions = value?.Assumptions ?? string.Empty;
                _ = LoadScenarioKpisAsync(value?.ScenarioId);
                RecalculateScenarioProbability();
            }
        }
    }

    public ScenarioKpiListItemViewModel? SelectedScenarioKpi
    {
        get => _selectedScenarioKpi;
        set
        {
            if (SetProperty(ref _selectedScenarioKpi, value))
            {
                DeleteScenarioKpiCommand.RaiseCanExecuteChanged();
                ScenarioKpiName = value?.KpiName ?? string.Empty;
                ScenarioKpiPeriod = value?.Period ?? string.Empty;
                ScenarioKpiValue = value?.ValueDisplay ?? string.Empty;
                ScenarioKpiUnit = value?.Unit ?? string.Empty;
                ScenarioKpiCurrency = value?.Currency ?? string.Empty;
                ScenarioKpiSnippetId = value?.SnippetId ?? string.Empty;
            }
        }
    }

    public string SelectedMetricNameFilter
    {
        get => _selectedMetricNameFilter;
        set
        {
            if (SetProperty(ref _selectedMetricNameFilter, value))
            {
                ApplyMetricFilters();
            }
        }
    }

    public string MetricPeriodFilter
    {
        get => _metricPeriodFilter;
        set
        {
            if (SetProperty(ref _metricPeriodFilter, value))
            {
                ApplyMetricFilters();
            }
        }
    }

    public string MetricEditName { get => _metricEditName; set { if (SetProperty(ref _metricEditName, value)) SaveMetricCommand.RaiseCanExecuteChanged(); } }
    public string MetricEditPeriod { get => _metricEditPeriod; set => SetProperty(ref _metricEditPeriod, value); }
    public string MetricEditValue { get => _metricEditValue; set => SetProperty(ref _metricEditValue, value); }
    public string MetricEditUnit { get => _metricEditUnit; set => SetProperty(ref _metricEditUnit, value); }
    public string MetricEditCurrency { get => _metricEditCurrency; set => SetProperty(ref _metricEditCurrency, value); }
    public string ScenarioName { get => _scenarioName; set { if (SetProperty(ref _scenarioName, value)) SaveScenarioCommand.RaiseCanExecuteChanged(); } }
    public string ScenarioProbability { get => _scenarioProbability; set => SetProperty(ref _scenarioProbability, value); }
    public string ScenarioAssumptions { get => _scenarioAssumptions; set => SetProperty(ref _scenarioAssumptions, value); }
    public string ScenarioKpiName { get => _scenarioKpiName; set { if (SetProperty(ref _scenarioKpiName, value)) SaveScenarioKpiCommand.RaiseCanExecuteChanged(); } }
    public string ScenarioKpiPeriod { get => _scenarioKpiPeriod; set { if (SetProperty(ref _scenarioKpiPeriod, value)) SaveScenarioKpiCommand.RaiseCanExecuteChanged(); } }
    public string ScenarioKpiValue { get => _scenarioKpiValue; set => SetProperty(ref _scenarioKpiValue, value); }
    public string ScenarioKpiUnit { get => _scenarioKpiUnit; set => SetProperty(ref _scenarioKpiUnit, value); }
    public string ScenarioKpiCurrency { get => _scenarioKpiCurrency; set => SetProperty(ref _scenarioKpiCurrency, value); }
    public string ScenarioKpiSnippetId { get => _scenarioKpiSnippetId; set => SetProperty(ref _scenarioKpiSnippetId, value); }
    public string ScenarioStatusMessage { get => _scenarioStatusMessage; set => SetProperty(ref _scenarioStatusMessage, value); }
    public string ScenarioProbabilitySummary => $"Total probability: {Scenarios.Sum(x => x.Probability):0.00}";
    public bool IsScenarioProbabilityWarning
    {
        get
        {
            var total = Scenarios.Sum(x => x.Probability);
            return total < 0.9 || total > 1.1;
        }
    }

    public string DocumentStatusMessage
    {
        get => _documentStatusMessage;
        set => SetProperty(ref _documentStatusMessage, value);
    }

    public string DetailsSummary
    {
        get => _detailsSummary;
        set => SetProperty(ref _detailsSummary, value);
    }

    public string DetailsTextPreview
    {
        get => _detailsTextPreview;
        set => SetProperty(ref _detailsTextPreview, value);
    }

    public string CompanyName
    {
        get => _companyName;
        set
        {
            if (SetProperty(ref _companyName, value))
            {
                SaveCompanyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CompanyTicker { get => _companyTicker; set => SetProperty(ref _companyTicker, value); }
    public string CompanyIsin { get => _companyIsin; set => SetProperty(ref _companyIsin, value); }
    public string CompanySector { get => _companySector; set => SetProperty(ref _companySector, value); }
    public string CompanyIndustry { get => _companyIndustry; set => SetProperty(ref _companyIndustry, value); }
    public string CompanyCurrency { get => _companyCurrency; set => SetProperty(ref _companyCurrency, value); }
    public string CompanySummary { get => _companySummary; set => SetProperty(ref _companySummary, value); }
    public string CompanyStatusMessage { get => _companyStatusMessage; set => SetProperty(ref _companyStatusMessage, value); }

    public string NewTagName
    {
        get => _newTagName;
        set
        {
            if (SetProperty(ref _newTagName, value))
            {
                AddTagCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NoteTitle
    {
        get => _noteTitle;
        set
        {
            if (SetProperty(ref _noteTitle, value))
            {
                SaveNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NoteContent { get => _noteContent; set => SetProperty(ref _noteContent, value); }
    public string SelectedNoteType { get => _selectedNoteType; set => SetProperty(ref _selectedNoteType, value); }
    public CompanyOptionViewModel? NotesFilterCompany
    {
        get => _notesFilterCompany;
        set
        {
            if (SetProperty(ref _notesFilterCompany, value))
            {
                ApplyNoteFilters();
            }
        }
    }

    public string NotesFilterType
    {
        get => _notesFilterType;
        set
        {
            if (SetProperty(ref _notesFilterType, value))
            {
                ApplyNoteFilters();
            }
        }
    }

    public string NoteStatusMessage { get => _noteStatusMessage; set => SetProperty(ref _noteStatusMessage, value); }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ExecuteSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public WorkspaceOptionViewModel? SelectedSearchWorkspace { get => _selectedSearchWorkspace; set => SetProperty(ref _selectedSearchWorkspace, value); }
    public CompanyOptionViewModel? SelectedSearchCompany { get => _selectedSearchCompany; set => SetProperty(ref _selectedSearchCompany, value); }
    public string SelectedSearchType { get => _selectedSearchType; set => SetProperty(ref _selectedSearchType, value); }
    public DateTime? SearchDateFrom { get => _searchDateFrom; set => SetProperty(ref _searchDateFrom, value); }
    public DateTime? SearchDateTo { get => _searchDateTo; set => SetProperty(ref _searchDateTo, value); }
    public string SearchStatusMessage { get => _searchStatusMessage; set => SetProperty(ref _searchStatusMessage, value); }

    public SearchResultListItemViewModel? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (SetProperty(ref _selectedSearchResult, value))
            {
                OpenSearchResultCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AskVaultQuery
    {
        get => _askVaultQuery;
        set
        {
            if (SetProperty(ref _askVaultQuery, value))
            {
                RetrieveAskVaultCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public CompanyOptionViewModel? SelectedAskVaultCompany
    {
        get => _selectedAskVaultCompany;
        set => SetProperty(ref _selectedAskVaultCompany, value);
    }

    public AskVaultContextItemViewModel? SelectedAskVaultContextItem
    {
        get => _selectedAskVaultContextItem;
        set
        {
            if (SetProperty(ref _selectedAskVaultContextItem, value))
            {
                OpenAskVaultContextItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AskVaultPrompt { get => _askVaultPrompt; set => SetProperty(ref _askVaultPrompt, value); }
    public string AskVaultStatusMessage { get => _askVaultStatusMessage; set => SetProperty(ref _askVaultStatusMessage, value); }


    public AgentTemplateListItemViewModel? SelectedAgentTemplate
    {
        get => _selectedAgentTemplate;
        set
        {
            if (SetProperty(ref _selectedAgentTemplate, value))
            {
                RunAgentCommand.RaiseCanExecuteChanged();
                _ = PopulateAgentTemplateFormAsync(value);
            }
        }
    }

    public AgentRunListItemViewModel? SelectedAgentRun
    {
        get => _selectedAgentRun;
        set
        {
            if (SetProperty(ref _selectedAgentRun, value))
            {
                _ = LoadRunNotebookAsync(value);
            }
        }
    }

    public ArtifactListItemViewModel? SelectedRunArtifact
    {
        get => _selectedRunArtifact;
        set
        {
            if (SetProperty(ref _selectedRunArtifact, value))
            {
                SaveArtifactCommand.RaiseCanExecuteChanged();
                _ = LoadEvidenceLinksForSelectedArtifactAsync(value);
                _ = RefreshArtifactEvidenceAsync();
            }
        }
    }

    public CompanyOptionViewModel? SelectedRunCompany { get => _selectedRunCompany; set => SetProperty(ref _selectedRunCompany, value); }

    public NotificationListItemViewModel? SelectedNotification
    {
        get => _selectedNotification;
        set
        {
            if (SetProperty(ref _selectedNotification, value))
            {
                MarkNotificationReadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ToastMessage { get => _toastMessage; set => SetProperty(ref _toastMessage, value); }
    public bool IsToastVisible { get => _isToastVisible; set => SetProperty(ref _isToastVisible, value); }
    public string AgentName { get => _agentName; set { if (SetProperty(ref _agentName, value)) { SaveAgentTemplateCommand.RaiseCanExecuteChanged(); } } }
    public string AgentGoal { get => _agentGoal; set => SetProperty(ref _agentGoal, value); }
    public string AgentInstructions { get => _agentInstructions; set => SetProperty(ref _agentInstructions, value); }
    public string AgentAllowedTools { get => _agentAllowedTools; set => SetProperty(ref _agentAllowedTools, value); }
    public string AgentOutputSchema { get => _agentOutputSchema; set => SetProperty(ref _agentOutputSchema, value); }
    public string AgentEvidencePolicy { get => _agentEvidencePolicy; set => SetProperty(ref _agentEvidencePolicy, value); }
    public string AgentQuery { get => _agentQuery; set => SetProperty(ref _agentQuery, value); }
    public string DataQualityStatusMessage { get => _dataQualityStatusMessage; set => SetProperty(ref _dataQualityStatusMessage, value); }
    public DataQualityEnrichmentSuggestionViewModel? SelectedEnrichmentSuggestion
    {
        get => _selectedEnrichmentSuggestion;
        set
        {
            if (SetProperty(ref _selectedEnrichmentSuggestion, value))
            {
                ApplySelectedEnrichmentSuggestionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DataQualityUnlinkedListItemViewModel? SelectedUnlinkedDocument
    {
        get => _selectedUnlinkedDocument;
        set
        {
            if (SetProperty(ref _selectedUnlinkedDocument, value))
            {
                LinkSelectedUnlinkedDocumentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DataQualityUnlinkedListItemViewModel? SelectedUnlinkedNote
    {
        get => _selectedUnlinkedNote;
        set
        {
            if (SetProperty(ref _selectedUnlinkedNote, value))
            {
                LinkSelectedUnlinkedNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public CompanyOptionViewModel? SelectedQualityCompany
    {
        get => _selectedQualityCompany;
        set
        {
            if (SetProperty(ref _selectedQualityCompany, value))
            {
                LinkSelectedUnlinkedDocumentCommand.RaiseCanExecuteChanged();
                LinkSelectedUnlinkedNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DataQualityDuplicateGroupViewModel? SelectedDuplicateGroup
    {
        get => _selectedDuplicateGroup;
        set
        {
            if (SetProperty(ref _selectedDuplicateGroup, value))
            {
                SelectedDuplicateKeepDocument = value?.Documents.FirstOrDefault();
                ArchiveDuplicateGroupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DataQualityDuplicateDocumentViewModel? SelectedDuplicateKeepDocument
    {
        get => _selectedDuplicateKeepDocument;
        set
        {
            if (SetProperty(ref _selectedDuplicateKeepDocument, value))
            {
                ArchiveDuplicateGroupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DataQualityArtifactGapViewModel? SelectedEvidenceGap
    {
        get => _selectedEvidenceGap;
        set
        {
            if (SetProperty(ref _selectedEvidenceGap, value))
            {
                OpenEvidenceGapCommand.RaiseCanExecuteChanged();
    public string AgentQuery
    {
        get => _agentQuery;
        set
        {
            if (SetProperty(ref _agentQuery, value))
            {
                RunAgentCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string AgentStatusMessage { get => _agentStatusMessage; set => SetProperty(ref _agentStatusMessage, value); }
    public string ImportInboxFolderPath { get => _importInboxFolderPath; set => SetProperty(ref _importInboxFolderPath, value); }
    public bool ImportInboxEnabled { get => _importInboxEnabled; set => SetProperty(ref _importInboxEnabled, value); }
    public string RunInputSummary { get => _runInputSummary; set => SetProperty(ref _runInputSummary, value); }
    public string RunToolCallsSummary { get => _runToolCallsSummary; set => SetProperty(ref _runToolCallsSummary, value); }
    public string AutomationStatusMessage { get => _automationStatusMessage; set => SetProperty(ref _automationStatusMessage, value); }
    public string EvidenceCoverageLabel
    {
        get => _evidenceCoverageLabel;
        private set => SetProperty(ref _evidenceCoverageLabel, value);
    }

    public bool HasEvidenceCoverage => ArtifactEvidenceLinks.Count > 0;
    public string ArtifactEvidenceStatusMessage { get => _artifactEvidenceStatusMessage; set => SetProperty(ref _artifactEvidenceStatusMessage, value); }
    public bool CanCreateSnippet => SelectedDocument is not null;

    public Task<IReadOnlyList<AgentTemplateRecord>> GetAgentTemplatesAsync(CancellationToken cancellationToken = default) => _agentService.GetAgentsAsync(cancellationToken);

    public Task<IReadOnlyList<CompanyRecord>> GetCompaniesAsync(CancellationToken cancellationToken = default) => _companyService.GetCompaniesAsync(cancellationToken);

    public async Task ImportDocumentsAsync(IEnumerable<string> filePaths)
    {
        var fileList = filePaths.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (fileList.Count == 0)
        {
            return;
        }

        var results = await _documentImportService.ImportFilesAsync(fileList);
        var successCount = results.Count(static r => r.Succeeded);
        var failures = results.Where(static r => !r.Succeeded).ToList();

        DocumentStatusMessage = failures.Count == 0
            ? $"Imported {successCount} document(s)."
            : $"Imported {successCount} document(s), {failures.Count} failed.";

        await LoadDocumentsAsync();
    }

    public async Task LoadDocumentsAsync()
    {
        var docs = await _documentImportService.GetDocumentsAsync();
        Documents.Clear();
        foreach (var doc in docs)
        {
            Documents.Add(new DocumentListItemViewModel
            {
                Id = doc.Id,
                Title = doc.Title,
                Type = doc.DocType,
                CompanyId = doc.CompanyId ?? string.Empty,
                Company = doc.CompanyName ?? string.Empty,
                PublishedDate = FormatDate(doc.PublishedAt),
                ImportedDate = FormatDate(doc.ImportedAt)
            });
        }

        RunSelectableDocuments.Clear();
        foreach (var item in Documents)
        {
            RunSelectableDocuments.Add(item);
        }
    }

    private async Task InitializeAsync()
    {
        await LoadCompaniesAndTagsAsync();
        await LoadDocumentsAsync();
        await LoadNotesAsync();
        await LoadSearchFiltersAsync();
        await EnsureAskMyVaultAgentAsync();
        await LoadAgentsAsync();
        await LoadAgentRunsAsync();
        await LoadDataQualityReportAsync();
        await LoadDashboardAsync();
        LoadAutomationTemplates();
        await LoadImportInboxSettingsAsync();
        await _importInboxWatcher.ReloadAsync();
    }


    private async Task LoadImportInboxSettingsAsync()
    {
        var settings = await _appSettingsService.GetSettingsAsync();
        ImportInboxFolderPath = settings.ImportInboxFolderPath;
        ImportInboxEnabled = settings.ImportInboxEnabled;
    }

    private async Task SaveImportInboxSettingsAsync()
    {
        var settings = await _appSettingsService.GetSettingsAsync();
        settings.ImportInboxFolderPath = ImportInboxFolderPath.Trim();
        settings.ImportInboxEnabled = ImportInboxEnabled;
        await _appSettingsService.SaveSettingsAsync(settings);
        await _importInboxWatcher.ReloadAsync();
        DocumentStatusMessage = "Import Inbox settings saved.";
    }

    private void OnImportInboxFileImported(object? sender, ImportInboxEvent e)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            DocumentStatusMessage = e.Succeeded
                ? $"Imported {e.FileName}"
                : $"Import failed for {e.FileName}: {e.ErrorMessage}";

            await LoadDocumentsAsync();
        await LoadNotificationsAsync();
        LoadConnectors();
        await LoadAutomationsAsync();
    }

    private async Task EnsureAskMyVaultAgentAsync()
    {
        var existing = (await _agentService.GetAgentsAsync()).FirstOrDefault(x => string.Equals(x.Name, "AskMyVault", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return;
        }

        await _agentService.CreateAgentAsync(new AgentTemplateUpsertRequest
        {
            Name = "AskMyVault",
            Goal = "Answer questions from local vault evidence",
            Instructions = "Use retrieved context and include citations like [DOC:<document_id>|chunk:<i>] and [SNIP:<id>] when available.",
            AllowedToolsJson = "[\"local_search\",\"prompt_build\"]",
            OutputSchema = "markdown",
            EvidencePolicy = "citation_required_when_available"
        });
    }


    private void LoadAutomationTemplates()
    {
        AutomationTemplates.Clear();
        foreach (var template in _automationTemplateService.GetTemplates())
        {
            AutomationTemplates.Add(new AutomationTemplateListItemViewModel
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                ScheduleSummary = template.ScheduleSummary,
                Payload = template.Payload
            });
        }
    }

    private void CreateAutomationFromTemplate(string templateId)
    {
        var automation = _automationTemplateService.CreateAutomationFromTemplate(templateId);
        Automations.Add(new AutomationListItemViewModel
        {
            Name = automation.Name,
            ScheduleSummary = automation.ScheduleSummary,
            Payload = automation.Payload
        });

        AutomationStatusMessage = $"Automation created: {automation.Name}";
    }
    private async Task SaveSelectedDocumentCompanyAsync()
    {
        if (SelectedDocument is null)
        {
            return;
        }

        await _documentImportService.UpdateDocumentCompanyAsync(SelectedDocument.Id, SelectedDocumentCompany?.Id);
        await LoadDocumentsAsync();
        DocumentStatusMessage = "Document-company link saved.";
    }

    private async Task LoadCompaniesAndTagsAsync()
    {
        var companies = await _companyService.GetCompaniesAsync();
        var tags = await _companyService.GetTagsAsync();

        Companies.Clear();
        foreach (var company in companies)
        {
            Companies.Add(new CompanyListItemViewModel
            {
                Id = company.Id,
                Name = company.Name,
                Ticker = company.Ticker ?? string.Empty,
                Isin = company.Isin ?? string.Empty,
                Sector = company.Sector ?? string.Empty,
                Industry = company.Industry ?? string.Empty,
                Currency = company.Currency ?? string.Empty,
                Summary = company.Summary ?? string.Empty,
                Tags = string.Join(", ", company.TagNames)
            });
        }

        CompanyOptions.Clear();
        foreach (var company in companies)
        {
            CompanyOptions.Add(new CompanyOptionViewModel { Id = company.Id, DisplayName = $"{company.Name} ({company.Ticker})".TrimEnd(' ', '(', ')') });
        }

        NoteFilterCompanies.Clear();
        NoteFilterCompanies.Add(new CompanyOptionViewModel { Id = string.Empty, DisplayName = "All companies" });
        foreach (var company in CompanyOptions)
        {
            NoteFilterCompanies.Add(new CompanyOptionViewModel { Id = company.Id, DisplayName = company.DisplayName });
        }
        NotesFilterCompany ??= NoteFilterCompanies.First();

        SelectedSearchCompany ??= NoteFilterCompanies.First();
        SelectedQualityCompany ??= CompanyOptions.FirstOrDefault();
        SelectedAskVaultCompany ??= NoteFilterCompanies.FirstOrDefault();

        AvailableTags.Clear();
        foreach (var tag in tags)
        {
            AvailableTags.Add(new TagSelectionViewModel { Id = tag.Id, Name = tag.Name });
        }

        SelectedHubCompany ??= CompanyOptions.FirstOrDefault();
    }

    private async Task RetrieveAskVaultPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(AskVaultQuery))
        {
            return;
        }

        AskVaultStatusMessage = "Retrieving context...";
        var result = await _askMyVaultService.BuildPreviewAsync(new AskMyVaultPreviewRequest
        {
            Query = AskVaultQuery,
            CompanyId = SelectedAskVaultCompany?.Id,
            MaxContextItems = 24
        });

        AskVaultContextItems.Clear();
        foreach (var item in result.ContextItems)
        {
            AskVaultContextItems.Add(new AskVaultContextItemViewModel
            {
                ResultType = item.ResultType,
                EntityId = item.EntityId,
                Title = item.Title,
                Excerpt = item.Excerpt,
                Citation = item.Citation,
                CompanyName = item.CompanyName
            });
        }

        SelectedAskVaultContextItem = AskVaultContextItems.FirstOrDefault();
        AskVaultPrompt = result.Prompt;
        AskVaultStatusMessage = $"Retrieved {AskVaultContextItems.Count} context item(s).";
    }

    private void OpenAskVaultContextItem(AskVaultContextItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (string.Equals(item.ResultType, "note", StringComparison.OrdinalIgnoreCase))
        {
            SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Notes", StringComparison.OrdinalIgnoreCase));
            SelectedNote = Notes.FirstOrDefault(n => n.Id == item.EntityId) ?? AllNotes.FirstOrDefault(n => n.Id == item.EntityId);
            AskVaultStatusMessage = SelectedNote is null
                ? "Note context selected, but it is not loaded in the current note list."
                : $"Opened note context: {item.Title}";
            return;
        }

        if (string.Equals(item.ResultType, "document", StringComparison.OrdinalIgnoreCase))
        {
            SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Documents", StringComparison.OrdinalIgnoreCase));
            SelectedDocument = Documents.FirstOrDefault(d => d.Id == item.EntityId);
            if (SelectedDocument is not null)
            {
                DetailsTextPreview = item.Excerpt;
                AskVaultStatusMessage = $"Opened document context: {item.Title}";
            }
            else
            {
                AskVaultStatusMessage = "Document context selected, but it is not available in the current document list.";
            }

            return;
        }

        if (string.Equals(item.ResultType, "snippet", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.ResultType, "artifact", StringComparison.OrdinalIgnoreCase))
        {
            SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Search", StringComparison.OrdinalIgnoreCase));
            SearchQuery = item.Title;
            AskVaultStatusMessage = $"{item.ResultType} context selected. Use Search to inspect full details.";
            return;
        }

        AskVaultStatusMessage = "Context item selected, but no direct navigation target is available yet.";
    }

    private void PopulateCompanyForm(CompanyListItemViewModel? company)
    {
        if (company is null)
        {
            ClearCompanyForm();
            return;
        }

        CompanyName = company.Name;
        CompanyTicker = company.Ticker;
        CompanyIsin = company.Isin;
        CompanySector = company.Sector;
        CompanyIndustry = company.Industry;
        CompanyCurrency = company.Currency;
        CompanySummary = company.Summary;

        var selectedTags = company.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in AvailableTags)
        {
            tag.IsSelected = selectedTags.Contains(tag.Name);
        }
    }

    private async Task SaveCompanyAsync()
    {
        var request = new CompanyUpsertRequest
        {
            Name = CompanyName.Trim(),
            Ticker = NullIfWhitespace(CompanyTicker),
            Isin = NullIfWhitespace(CompanyIsin),
            Sector = NullIfWhitespace(CompanySector),
            Industry = NullIfWhitespace(CompanyIndustry),
            Currency = NullIfWhitespace(CompanyCurrency),
            Summary = NullIfWhitespace(CompanySummary)
        };

        var selectedTagIds = AvailableTags.Where(static x => x.IsSelected).Select(x => x.Id).ToList();

        if (SelectedCompany is null)
        {
            await _companyService.CreateCompanyAsync(request, selectedTagIds);
            CompanyStatusMessage = "Company created.";
        }
        else
        {
            await _companyService.UpdateCompanyAsync(SelectedCompany.Id, request, selectedTagIds);
            CompanyStatusMessage = "Company updated.";
        }

        await LoadCompaniesAndTagsAsync();
        await LoadDocumentsAsync();
        await LoadNotesAsync();
    }

    private async Task DeleteCompanyAsync()
    {
        if (SelectedCompany is null)
        {
            return;
        }

        await _companyService.DeleteCompanyAsync(SelectedCompany.Id);
        CompanyStatusMessage = "Company deleted.";
        SelectedCompany = null;
        await LoadCompaniesAndTagsAsync();
        await LoadDocumentsAsync();
        await LoadNotesAsync();
    }

    private async Task AddTagAsync()
    {
        await _companyService.CreateTagAsync(NewTagName);
        NewTagName = string.Empty;
        await LoadCompaniesAndTagsAsync();
        CompanyStatusMessage = "Tag created.";
    }

    private void ClearCompanyForm()
    {
        SelectedCompany = null;
        CompanyName = string.Empty;
        CompanyTicker = string.Empty;
        CompanyIsin = string.Empty;
        CompanySector = string.Empty;
        CompanyIndustry = string.Empty;
        CompanyCurrency = string.Empty;
        CompanySummary = string.Empty;
        foreach (var tag in AvailableTags)
        {
            tag.IsSelected = false;
        }
    }

    private async Task LoadNotesAsync()
    {
        var notes = await _noteService.GetNotesAsync();
        AllNotes.Clear();
        foreach (var note in notes)
        {
            AllNotes.Add(new NoteListItemViewModel
            {
                Id = note.Id,
                Title = note.Title,
                Content = note.Content,
                NoteType = note.NoteType,
                CompanyId = note.CompanyId ?? string.Empty,
                CompanyName = note.CompanyName ?? string.Empty,
                CreatedAt = FormatDate(note.CreatedAt)
            });
        }

        ApplyNoteFilters();
    }

    private void PopulateNoteForm(NoteListItemViewModel? note)
    {
        if (note is null)
        {
            ClearNoteForm();
            return;
        }

        NoteTitle = note.Title;
        NoteContent = note.Content;
        SelectedNoteType = note.NoteType;
        SelectedNoteCompany = CompanyOptions.FirstOrDefault(c => c.Id == note.CompanyId);
    }

    private async Task SaveNoteAsync()
    {
        var request = new NoteUpsertRequest
        {
            Title = NoteTitle.Trim(),
            Content = NoteContent,
            NoteType = SelectedNoteType,
            CompanyId = SelectedNoteCompany?.Id
        };

        if (SelectedNote is null)
        {
            await _noteService.CreateNoteAsync(request);
            NoteStatusMessage = "Note created.";
        }
        else
        {
            await _noteService.UpdateNoteAsync(SelectedNote.Id, request);
            NoteStatusMessage = "Note updated.";
        }

        await LoadNotesAsync();
    }

    private async Task DeleteNoteAsync()
    {
        if (SelectedNote is null)
        {
            return;
        }

        await _noteService.DeleteNoteAsync(SelectedNote.Id);
        NoteStatusMessage = "Note deleted.";
        ClearNoteForm();
        await LoadNotesAsync();
    }

    private void ClearNoteForm()
    {
        _selectedNote = null;
        OnPropertyChanged(nameof(SelectedNote));
        NoteTitle = string.Empty;
        NoteContent = string.Empty;
        SelectedNoteType = "manual";
        SelectedNoteCompany = null;
    }

    public async Task ImportAiOutputAsync(AiImportRequest request)
    {
        await _noteService.ImportAiOutputAsync(request);
        NoteStatusMessage = "AI output imported.";
        await LoadNotesAsync();
    }

    private void ApplyNoteFilters()
    {
        var filtered = AllNotes.Where(n =>
            (NotesFilterCompany is null || string.IsNullOrWhiteSpace(NotesFilterCompany.Id) || string.Equals(n.CompanyId, NotesFilterCompany.Id, StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(NotesFilterType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(n.NoteType, NotesFilterType, StringComparison.OrdinalIgnoreCase)));

        Notes.Clear();
        foreach (var note in filtered)
        {
            Notes.Add(note);
        }
    }

    private async Task SaveMetricAsync()
    {
        if (SelectedHubCompany is null)
        {
            MetricStatusMessage = "Select a company first.";
            return;
        }

        if (!double.TryParse(MetricValue, out var parsedValue))
        {
            MetricStatusMessage = "Metric value must be a number.";
            return;
        }

        var request = new MetricUpsertRequest
        {
            CompanyId = SelectedHubCompany.Id,
            MetricName = MetricName,
            Period = MetricPeriod,
            Value = parsedValue,
            Unit = MetricUnit,
            Currency = MetricCurrency
        };

        var result = await _metricService.UpsertMetricAsync(request);
        if (result.Status == MetricUpsertStatus.ConflictDetected)
        {
            var choice = _metricConflictDialogService.ShowMetricConflictDialog();
            if (choice == MetricConflictDialogChoice.Cancel)
            {
                MetricStatusMessage = "Metric save canceled.";
                return;
            }

            var resolution = choice == MetricConflictDialogChoice.Replace
                ? MetricConflictResolution.ReplaceExisting
                : MetricConflictResolution.CreateAnyway;

            result = await _metricService.UpsertMetricAsync(request, resolution);
        }

        MetricStatusMessage = result.Status switch
        {
            MetricUpsertStatus.Replaced => $"Metric '{result.NormalizedMetricName}' replaced.",
            MetricUpsertStatus.CreatedAnyway => $"Metric '{result.NormalizedMetricName}' saved as an additional entry.",
            _ => $"Metric '{result.NormalizedMetricName}' saved."
        };

        await LoadCompanyHubAsync(SelectedHubCompany.Id);
    }

    private async Task LoadCompanyHubAsync(string? companyId)
    {
        HubDocuments.Clear();
        HubNotes.Clear();
        HubEvents.Clear();
        HubMetrics.Clear();
        Scenarios.Clear();
        ScenarioKpis.Clear();
        AllHubMetrics.Clear();
        HubPrices.Clear();
        LatestClosePrice = "-";
        LatestCloseDate = "-";
        MetricNameOptions.Clear();
        MetricNameOptions.Add("All");
        MetricPeriodFilter = string.Empty;
        HubAgentRuns.Clear();

        if (string.IsNullOrWhiteSpace(companyId))
        {
            SelectedHubMetric = null;
            SelectedScenario = null;
            SelectedScenarioKpi = null;
            return;
        }

        var docs = await _companyService.GetCompanyDocumentsAsync(companyId);
        var notes = await _companyService.GetCompanyNotesAsync(companyId);
        var eventsList = await _companyService.GetCompanyEventsAsync(companyId);
        var metrics = await _companyService.GetCompanyMetricsAsync(companyId);
        var runs = await _companyService.GetCompanyAgentRunsAsync(companyId);
        var scenarios = await _companyService.GetCompanyScenariosAsync(companyId);
        var latestPrice = await _companyService.GetLatestCompanyPriceAsync(companyId);
        var dailyPrices = await _companyService.GetCompanyDailyPricesAsync(companyId, 90);

        foreach (var doc in docs)
        {
            HubDocuments.Add($"{doc.Title} ({doc.DocType})");
        }

        foreach (var note in notes)
        {
            HubNotes.Add(note.Title);
        }

        foreach (var item in eventsList)
        {
            HubEvents.Add(item);
        }

        foreach (var item in metrics)
        {
            AllHubMetrics.Add(new CompanyMetricListItemViewModel
            {
                Id = item.Id,
                MetricName = item.MetricName,
                Period = item.Period,
                ValueDisplay = item.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Unit = item.Unit ?? string.Empty,
                Currency = item.Currency ?? string.Empty,
                EvidenceDisplay = BuildEvidenceDisplay(item),
                CreatedAt = FormatDate(item.CreatedAt),
                DocumentId = item.DocumentId,
                SnippetId = item.SnippetId,
                Locator = item.Locator,
                SourceTitle = item.SourceTitle,
                SnippetText = item.SnippetText,
                Value = item.Value
            });
        }

        foreach (var item in scenarios)
        {
            Scenarios.Add(new ScenarioListItemViewModel
            {
                ScenarioId = item.ScenarioId,
                Name = item.Name,
                Probability = item.Probability,
                Assumptions = item.Assumptions ?? string.Empty
            });
        }

        foreach (var price in dailyPrices)
        {
            HubPrices.Add(new PriceDailyListItemViewModel
            {
                PriceDate = price.PriceDate,
                CloseDisplay = price.Close.ToString("0.####", CultureInfo.InvariantCulture),
                Currency = price.Currency,
                SourceId = price.SourceId
            });
        }

        if (latestPrice is not null)
        {
            LatestClosePrice = $"{latestPrice.Close.ToString("0.####", CultureInfo.InvariantCulture)} {latestPrice.Currency}";
            LatestCloseDate = latestPrice.PriceDate;
        }

        foreach (var metricName in await _companyService.GetCompanyMetricNamesAsync(companyId))
        {
            MetricNameOptions.Add(metricName);
        }

        SelectedMetricNameFilter = MetricNameOptions.FirstOrDefault() ?? "All";
        ApplyMetricFilters();
        SelectedHubMetric = HubMetrics.FirstOrDefault();
        SelectedScenario = Scenarios.FirstOrDefault();
        OnPropertyChanged(nameof(ScenarioProbabilitySummary));
        OnPropertyChanged(nameof(IsScenarioProbabilityWarning));

        foreach (var item in runs)
        {
            HubAgentRuns.Add(item);
        }
    }

    private void ApplyMetricFilters()
    {
        var filtered = AllHubMetrics.Where(metric =>
            (string.IsNullOrWhiteSpace(SelectedMetricNameFilter) || string.Equals(SelectedMetricNameFilter, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(metric.MetricName, SelectedMetricNameFilter, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(MetricPeriodFilter) || metric.Period.Contains(MetricPeriodFilter, StringComparison.OrdinalIgnoreCase)));

        HubMetrics.Clear();
        foreach (var metric in filtered)
        {
            HubMetrics.Add(metric);
        }

        if (SelectedHubMetric is not null && HubMetrics.All(m => m.Id != SelectedHubMetric.Id))
        {
            SelectedHubMetric = HubMetrics.FirstOrDefault();
        }
    }

    private async Task SaveSelectedMetricAsync()
    {
        if (SelectedHubMetric is null)
        {
            return;
        }

        double? parsed = null;
        if (!string.IsNullOrWhiteSpace(MetricEditValue))
        {
            if (!double.TryParse(MetricEditValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
            {
                CompanyStatusMessage = "Metric value must be numeric.";
                return;
            }

            parsed = parsedValue;
        }

        await _companyService.UpdateCompanyMetricAsync(SelectedHubMetric.Id, new CompanyMetricUpdateRequest
        {
            MetricName = MetricEditName,
            Period = MetricEditPeriod,
            Value = parsed,
            Unit = MetricEditUnit,
            Currency = MetricEditCurrency
        });

        CompanyStatusMessage = "Metric updated.";
        await LoadCompanyHubAsync(SelectedHubCompany?.Id);
    }

    private async Task DeleteSelectedMetricAsync()
    {
        if (SelectedHubMetric is null)
        {
            return;
        }

        await _companyService.DeleteCompanyMetricAsync(SelectedHubMetric.Id);
        CompanyStatusMessage = "Metric deleted.";
        await LoadCompanyHubAsync(SelectedHubCompany?.Id);
    }

    private async Task OpenMetricEvidenceAsync(CompanyMetricListItemViewModel? metric)
    {
        if (metric is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(metric.DocumentId))
        {
            SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Documents", StringComparison.OrdinalIgnoreCase));
            await LoadDocumentsAsync();
            SelectedDocument = Documents.FirstOrDefault(d => d.Id == metric.DocumentId);
            if (!string.IsNullOrWhiteSpace(metric.SnippetText))
            {
                DetailsTextPreview = metric.SnippetText;
            }

            DocumentStatusMessage = "Opened metric evidence in Documents (best-effort snippet highlight).";
            return;
        }

        var sourceLine = string.IsNullOrWhiteSpace(metric.SourceTitle) ? "(unknown)" : metric.SourceTitle;
        _dialogService.ShowInfo(
            $"Snippet: {metric.SnippetText}{Environment.NewLine}{Environment.NewLine}Locator: {metric.Locator}{Environment.NewLine}Source: {sourceLine}",
            "Metric Evidence Snippet");
    }

    private static string BuildEvidenceDisplay(CompanyMetricRecord item)
    {
        var locator = string.IsNullOrWhiteSpace(item.Locator) ? "(no locator)" : item.Locator;
        if (!string.IsNullOrWhiteSpace(item.DocumentTitle))
        {
            return $"{item.DocumentTitle} — {locator}";
        }

        if (!string.IsNullOrWhiteSpace(item.SourceTitle))
        {
            return $"{item.SourceTitle} — {locator}";
        }

        return locator;
    }

    private async Task SaveScenarioAsync()
    {
        if (SelectedHubCompany is null)
        {
            return;
        }

        if (!double.TryParse(ScenarioProbability, NumberStyles.Any, CultureInfo.InvariantCulture, out var probability))
        {
            ScenarioStatusMessage = "Scenario probability must be numeric.";
            return;
        }

        var request = new ScenarioUpsertRequest
        {
            Name = ScenarioName,
            Probability = probability,
            Assumptions = ScenarioAssumptions
        };

        if (SelectedScenario is null)
        {
            await _companyService.CreateScenarioAsync(SelectedHubCompany.Id, request);
            ScenarioStatusMessage = "Scenario created.";
        }
        else
        {
            await _companyService.UpdateScenarioAsync(SelectedScenario.ScenarioId, request);
            ScenarioStatusMessage = "Scenario updated.";
        }

        await LoadCompanyHubAsync(SelectedHubCompany.Id);
    }

    private async Task DeleteScenarioAsync()
    {
        if (SelectedScenario is null || SelectedHubCompany is null)
        {
            return;
        }

        await _companyService.DeleteScenarioAsync(SelectedScenario.ScenarioId);
        ScenarioStatusMessage = "Scenario deleted.";
        await LoadCompanyHubAsync(SelectedHubCompany.Id);
    }

    private async Task LoadScenarioKpisAsync(string? scenarioId)
    {
        ScenarioKpis.Clear();
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            return;
        }

        var kpis = await _companyService.GetScenarioKpisAsync(scenarioId);
        foreach (var item in kpis)
        {
            ScenarioKpis.Add(new ScenarioKpiListItemViewModel
            {
                ScenarioKpiId = item.ScenarioKpiId,
                KpiName = item.KpiName,
                Period = item.Period,
                ValueDisplay = item.Value.ToString(CultureInfo.InvariantCulture),
                Unit = item.Unit ?? string.Empty,
                Currency = item.Currency ?? string.Empty,
                SnippetId = item.SnippetId ?? string.Empty
            });
        }

        SelectedScenarioKpi = ScenarioKpis.FirstOrDefault();
    }

    private async Task SaveScenarioKpiAsync()
    {
        if (SelectedScenario is null)
        {
            return;
        }

        if (!double.TryParse(ScenarioKpiValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            ScenarioStatusMessage = "Scenario KPI value must be numeric.";
            return;
        }

        var request = new ScenarioKpiUpsertRequest
        {
            KpiName = ScenarioKpiName,
            Period = ScenarioKpiPeriod,
            Value = value,
            Unit = ScenarioKpiUnit,
            Currency = ScenarioKpiCurrency,
            SnippetId = ScenarioKpiSnippetId
        };

        if (SelectedScenarioKpi is null)
        {
            await _companyService.CreateScenarioKpiAsync(SelectedScenario.ScenarioId, request);
            ScenarioStatusMessage = "Scenario KPI created.";
        }
        else
        {
            await _companyService.UpdateScenarioKpiAsync(SelectedScenarioKpi.ScenarioKpiId, request);
            ScenarioStatusMessage = "Scenario KPI updated.";
        }

        await LoadScenarioKpisAsync(SelectedScenario.ScenarioId);
    }

    private async Task DeleteScenarioKpiAsync()
    {
        if (SelectedScenarioKpi is null || SelectedScenario is null)
        {
            return;
        }

        await _companyService.DeleteScenarioKpiAsync(SelectedScenarioKpi.ScenarioKpiId);
        ScenarioStatusMessage = "Scenario KPI deleted.";
        await LoadScenarioKpisAsync(SelectedScenario.ScenarioId);
    }

    private void RecalculateScenarioProbability()
    {
        OnPropertyChanged(nameof(ScenarioProbabilitySummary));
        OnPropertyChanged(nameof(IsScenarioProbabilityWarning));
    }

    private async Task LoadDocumentDetailsAsync(string? documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            DetailsSummary = "Select a document to view metadata.";
            DetailsTextPreview = string.Empty;
            _selectedDocumentWorkspaceId = string.Empty;
            DocumentSnippets.Clear();
            return;
        }

        var detail = await _documentImportService.GetDocumentDetailsAsync(documentId);
        if (detail is null)
        {
            DetailsSummary = "Document not found.";
            DetailsTextPreview = string.Empty;
            _selectedDocumentWorkspaceId = string.Empty;
            DocumentSnippets.Clear();
            return;
        }

        _selectedDocumentWorkspaceId = detail.WorkspaceId;

        DetailsSummary =
            $"Title: {detail.Title}{Environment.NewLine}" +
            $"Type: {detail.DocType}{Environment.NewLine}" +
            $"Company: {(string.IsNullOrWhiteSpace(detail.CompanyName) ? "—" : detail.CompanyName)}{Environment.NewLine}" +
            $"Published: {FormatDate(detail.PublishedAt)}{Environment.NewLine}" +
            $"Imported: {FormatDate(detail.ImportedAt)}{Environment.NewLine}" +
            $"Path: {detail.FilePath}{Environment.NewLine}" +
            $"Hash: {detail.ContentHash}";

        DetailsTextPreview = string.IsNullOrWhiteSpace(detail.ExtractedText)
            ? "No extracted text available for this document type."
            : detail.ExtractedText;

        await LoadSnippetsForDocumentAsync(documentId);
    }

    public async Task CreateSnippetForSelectedDocumentAsync(string locator, string snippetText, string? companyId)
    {
        if (SelectedDocument is null)
        {
            DocumentStatusMessage = "Select a document before creating a snippet.";
            return;
        }

        if (string.IsNullOrWhiteSpace(locator))
        {
            DocumentStatusMessage = "Snippet locator is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(snippetText))
        {
            DocumentStatusMessage = "Snippet text is required.";
            return;
        }

        if (snippetText.Trim().Length < 10)
        {
            DocumentStatusMessage = "Snippet text must be at least 10 characters.";
            return;
        }

        var workspaceId = _selectedDocumentWorkspaceId;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            var workspaces = await _searchService.GetWorkspacesAsync();
            workspaceId = workspaces.FirstOrDefault()?.Id ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            DocumentStatusMessage = "Unable to resolve workspace for snippet creation.";
            return;
        }

        await _evidenceService.CreateSnippetAsync(
            workspaceId,
            SelectedDocument.Id,
            string.IsNullOrWhiteSpace(companyId) ? null : companyId,
            sourceId: null,
            locator,
            snippetText,
            createdBy: "user");

        await LoadSnippetsForDocumentAsync(SelectedDocument.Id);
        DocumentStatusMessage = "Snippet created.";
    }

    private async Task LoadSnippetsForDocumentAsync(string documentId)
    {
        var snippets = await _evidenceService.ListSnippetsByDocumentAsync(documentId);
        DocumentSnippets.Clear();
        foreach (var snippet in snippets)
        {
            DocumentSnippets.Add(new DocumentSnippetListItemViewModel
            {
                Id = snippet.Id,
                CompanyId = snippet.CompanyId,
                DocumentTitle = SelectedDocument?.Title ?? string.Empty,
                Locator = snippet.Locator,
                Text = snippet.Text,
                CreatedAt = FormatDate(snippet.CreatedAt)
            });
        }
    }

    public async Task CreateMetricFromSnippetAsync(string snippetId, string companyId, string metricName, string period, double value, string? unit, string? currency)
    {
        if (string.IsNullOrWhiteSpace(snippetId) || string.IsNullOrWhiteSpace(companyId) || string.IsNullOrWhiteSpace(metricName) || string.IsNullOrWhiteSpace(period))
        {
            DocumentStatusMessage = "Metric fields are required.";
            return;
        }

        await _metricService.CreateMetricAsync(new MetricCreateRequest
        {
            SnippetId = snippetId,
            CompanyId = companyId,
            MetricName = metricName,
            Period = period,
            Value = value,
            Unit = unit,
            Currency = currency
        });

        DocumentStatusMessage = "Metric created from snippet.";
        if (SelectedHubCompany?.Id == companyId)
        {
            await LoadCompanyHubAsync(companyId);
        }
    }

    private async Task LoadSearchFiltersAsync()
    {
        var workspaces = await _searchService.GetWorkspacesAsync();
        WorkspaceOptions.Clear();
        WorkspaceOptions.Add(new WorkspaceOptionViewModel { Id = string.Empty, Name = "All workspaces" });
        foreach (var workspace in workspaces)
        {
            WorkspaceOptions.Add(new WorkspaceOptionViewModel { Id = workspace.Id, Name = workspace.Name });
        }

        SelectedSearchWorkspace ??= WorkspaceOptions.FirstOrDefault();
    }

    private async Task ExecuteSearchAsync()
    {
        var query = new SearchQuery
        {
            QueryText = SearchQuery,
            WorkspaceId = string.IsNullOrWhiteSpace(SelectedSearchWorkspace?.Id) ? null : SelectedSearchWorkspace.Id,
            CompanyId = string.IsNullOrWhiteSpace(SelectedSearchCompany?.Id) ? null : SelectedSearchCompany.Id,
            Type = SelectedSearchType,
            DateFromIso = SearchDateFrom?.Date.ToString("O"),
            DateToIso = SearchDateTo?.Date.AddDays(1).AddTicks(-1).ToString("O")
        };

        var results = await _searchService.SearchAsync(query);
        SearchResults.Clear();
        foreach (var result in results)
        {
            SearchResults.Add(new SearchResultListItemViewModel
            {
                ResultType = result.ResultType,
                EntityId = result.EntityId,
                Title = result.Title,
                CompanyName = result.CompanyName ?? string.Empty,
                OccurredAt = FormatDate(result.OccurredAt),
                Snippet = StripHighlight(result.MatchSnippet)
            });
        }

        SearchStatusMessage = $"Found {results.Count} result(s).";
    }


    private async Task LoadAgentsAsync()
    {
        var agents = await _agentService.GetAgentsAsync();
        AgentTemplates.Clear();
        foreach (var agent in agents)
        {
            AgentTemplates.Add(new AgentTemplateListItemViewModel
            {
                Id = agent.Id,
                Name = agent.Name,
                Goal = agent.Goal,
                EvidencePolicy = agent.EvidencePolicy
            });
        }
    }

    private async Task LoadAgentRunsAsync()
    {
        var runs = await _agentService.GetRunsAsync();
        AgentRuns.Clear();
        foreach (var run in runs)
        {
            AgentRuns.Add(new AgentRunListItemViewModel
            {
                Id = run.Id,
                AgentName = run.AgentName,
                CompanyId = run.CompanyId ?? string.Empty,
                CompanyName = run.CompanyName ?? string.Empty,
                Query = run.Query,
                Status = run.Status,
                StartedAt = FormatDate(run.StartedAt),
                SelectedDocumentIdsJson = run.SelectedDocumentIdsJson
            });
        }
    }

    private async Task SaveAgentTemplateAsync()
    {
        var request = new AgentTemplateUpsertRequest
        {
            Name = AgentName,
            Goal = AgentGoal,
            Instructions = AgentInstructions,
            AllowedToolsJson = AgentAllowedTools,
            OutputSchema = AgentOutputSchema,
            EvidencePolicy = AgentEvidencePolicy
        };

        if (SelectedAgentTemplate is null)
        {
            await _agentService.CreateAgentAsync(request);
            AgentStatusMessage = "Agent template created.";
        }
        else
        {
            await _agentService.UpdateAgentAsync(SelectedAgentTemplate.Id, request);
            AgentStatusMessage = "Agent template updated.";
        }

        await LoadAgentsAsync();
    }

    private void ClearAgentTemplateForm()
    {
        _selectedAgentTemplate = null;
        OnPropertyChanged(nameof(SelectedAgentTemplate));
        AgentName = string.Empty;
        AgentGoal = string.Empty;
        AgentInstructions = string.Empty;
        AgentAllowedTools = "[]";
        AgentOutputSchema = string.Empty;
        AgentEvidencePolicy = string.Empty;
    }

    private async Task PopulateAgentTemplateFormAsync(AgentTemplateListItemViewModel? template)
    {
        if (template is null)
        {
            return;
        }

        var full = (await _agentService.GetAgentsAsync()).FirstOrDefault(x => x.Id == template.Id);
        if (full is null)
        {
            return;
        }

        AgentName = full.Name;
        AgentGoal = full.Goal;
        AgentInstructions = full.Instructions;
        AgentAllowedTools = full.AllowedToolsJson;
        AgentOutputSchema = full.OutputSchema;
        AgentEvidencePolicy = full.EvidencePolicy;
    }

    private async Task RunAgentAsync()
    {
        var askMyVault = SelectedAgentTemplate;
        if (askMyVault is null || !string.Equals(askMyVault.Name, "AskMyVault", StringComparison.OrdinalIgnoreCase))
        {
            askMyVault = AgentTemplates.FirstOrDefault(x => string.Equals(x.Name, "AskMyVault", StringComparison.OrdinalIgnoreCase));
            if (askMyVault is null)
            {
                await EnsureAskMyVaultAgentAsync();
                await LoadAgentsAsync();
                askMyVault = AgentTemplates.FirstOrDefault(x => string.Equals(x.Name, "AskMyVault", StringComparison.OrdinalIgnoreCase));
            }
        }

        if (askMyVault is null)
        {
            AgentStatusMessage = "Unable to find AskMyVault template.";
            return;
        }

        var selectedDocIds = RunSelectableDocuments
            .Where(d => d.IsSelected)
            .Select(d => d.Id)
            .Distinct()
            .ToList();

        if (SelectedAgentTemplate is not null)
        {
            var runId = await _agentService.CreateRunAsync(new AgentRunRequest
            {
                AgentId = SelectedAgentTemplate.Id,
                CompanyId = SelectedRunCompany?.Id,
                Query = AgentQuery,
                SelectedDocumentIds = selectedDocIds
            });

            AgentStatusMessage = "Run executed and output artifact captured.";
            await LoadAgentRunsAsync();
            SelectedAgentRun = AgentRuns.FirstOrDefault(x => x.Id == runId);
            return;
        }

        var askResult = await _agentService.ExecuteAskMyVaultAsync(new AskMyVaultRequest
        {
            AgentId = askMyVault.Id,
            CompanyId = SelectedRunCompany?.Id,
            Query = AgentQuery,
            SelectedDocumentIds = selectedDocIds
        });

        await LoadAgentRunsAsync();
        SelectedAgentRun = AgentRuns.FirstOrDefault(x => x.Id == runId);

        var run = (await _agentService.GetRunsAsync(SelectedAgentTemplate.Id)).FirstOrDefault(x => x.Id == runId);
        var wasSuccessful = string.Equals(run?.Status, "success", StringComparison.OrdinalIgnoreCase);
        var title = wasSuccessful
            ? $"Automation '{SelectedAgentTemplate.Name}' completed"
            : $"Automation '{SelectedAgentTemplate.Name}' failed";
        var body = wasSuccessful
            ? "Output artifact captured."
            : string.IsNullOrWhiteSpace(run?.Error)
                ? "Automation failed with an unknown error."
                : run.Error;

        await _notificationService.AddNotification(wasSuccessful ? "info" : "error", title, body);
        await LoadNotificationsAsync();
        ShowToast(title);

        AgentStatusMessage = wasSuccessful
            ? "Run executed and output artifact captured."
            : $"Run failed: {body}";
        AgentStatusMessage = "Answer generated and run history captured.";
        await LoadAgentRunsAsync();
        SelectedAgentRun = AgentRuns.FirstOrDefault(x => x.Id == runId);
        var toolCalls = await _agentService.GetToolCallsAsync(runId);
        if (toolCalls.Any(x => x.Name == "citation_parse" && x.OutputJson.Contains("\"citationCount\":0", StringComparison.OrdinalIgnoreCase)))
        {
            AgentStatusMessage = "Answer generated and run history captured. No citations detected.";
        }
        AgentStatusMessage = askResult.CitationsDetected
            ? "Answer generated and stored in run history."
            : "Answer generated and stored in run history. No citations detected.";
        await LoadAgentRunsAsync();
        SelectedAgentRun = AgentRuns.FirstOrDefault(x => x.Id == askResult.RunId);
    }

    private async Task LoadRunNotebookAsync(AgentRunListItemViewModel? run)
    {
        RunArtifacts.Clear();
        ArtifactEvidenceLinks.Clear();
        EvidenceCoverageLabel = "0 evidence links";
        OnPropertyChanged(nameof(HasEvidenceCoverage));
        if (run is null)
        {
            RunInputSummary = "Select a run to view notebook details.";
            ArtifactEvidenceStatusMessage = "Select an artifact to view linked evidence.";
            return;
        }

        var selectedDocIds = JsonSerializer.Deserialize<List<string>>(run.SelectedDocumentIdsJson) ?? [];
        RunInputSummary = $"Query: {run.Query}{Environment.NewLine}Selected docs: {(selectedDocIds.Count == 0 ? "(none)" : string.Join(", ", selectedDocIds))}";
        var toolCalls = await _agentService.GetToolCallsAsync(run.Id);
        RunToolCallsSummary = toolCalls.Count == 0
            ? "No tool calls captured."
            : string.Join(Environment.NewLine, toolCalls.Select(tc => $"{tc.Name} [{tc.Status}]"));
            ? "No tool calls recorded."
            : string.Join(Environment.NewLine, toolCalls.Select(x => $"{x.Name} ({x.Status})"));

        var artifacts = await _agentService.GetArtifactsAsync(run.Id);
        foreach (var artifact in artifacts)
        {
            RunArtifacts.Add(new ArtifactListItemViewModel { Id = artifact.Id, Title = artifact.Title, Content = artifact.Content });
        }

        SelectedRunArtifact = RunArtifacts.FirstOrDefault();
    }

    private async Task LoadEvidenceLinksForSelectedArtifactAsync(ArtifactListItemViewModel? artifact)
    {
        ArtifactEvidenceLinks.Clear();
        if (artifact is null)
        {
            EvidenceCoverageLabel = "0 evidence links";
            OnPropertyChanged(nameof(HasEvidenceCoverage));
            return;
        }

        var evidenceLinks = await _evidenceService.ListEvidenceLinksByArtifactAsync(artifact.Id);
        foreach (var link in evidenceLinks)
        {
            var locatorValue = string.IsNullOrWhiteSpace(link.Locator) ? "(missing locator)" : link.Locator.Trim();
            var snippetIdValue = string.IsNullOrWhiteSpace(link.SnippetId) ? "-" : link.SnippetId.Trim();
            var title = string.IsNullOrWhiteSpace(link.DocumentTitle) ? "Untitled document" : link.DocumentTitle.Trim();

            ArtifactEvidenceLinks.Add(new ArtifactEvidenceLinkListItemViewModel
            {
                Id = link.Id,
                DocumentTitle = title,
                Locator = locatorValue,
                SnippetId = snippetIdValue,
                Quote = string.IsNullOrWhiteSpace(link.Quote) ? link.SnippetText ?? string.Empty : link.Quote,
                HasMissingLocator = string.IsNullOrWhiteSpace(link.Locator),
                Citation = $"[{title} | {locatorValue} | {snippetIdValue}]"
            });
        }

        EvidenceCoverageLabel = ArtifactEvidenceLinks.Count == 1
            ? "1 evidence link"
            : $"{ArtifactEvidenceLinks.Count} evidence links";
        OnPropertyChanged(nameof(HasEvidenceCoverage));
    }

    private async Task SaveArtifactAsync()
    {
        if (SelectedRunArtifact is null)
        {
            return;
        }

        await _agentService.UpdateArtifactContentAsync(SelectedRunArtifact.Id, SelectedRunArtifact.Content);
        AgentStatusMessage = "Artifact output saved.";
    }

    private async Task LoadNotificationsAsync(bool unreadOnly = false)
    {
        var workspaceId = SelectedSearchWorkspace?.Id ?? WorkspaceOptions.FirstOrDefault()?.Id ?? string.Empty;
        var notifications = await _notificationService.ListNotifications(workspaceId, unreadOnly);
        Notifications.Clear();
        foreach (var notification in notifications)
        {
            Notifications.Add(new NotificationListItemViewModel
            {
                NotificationId = notification.NotificationId,
                Level = notification.Level,
                Title = notification.Title,
                Body = notification.Body,
                CreatedAt = FormatDate(notification.CreatedAt),
                IsRead = notification.IsRead
            });
        }
    }

    private async Task MarkNotificationReadAsync()
    {
        if (SelectedNotification is null || SelectedNotification.IsRead)
    private async Task LoadAutomationsAsync()
    {
        var records = await _automationService.GetAutomationsAsync();
        Automations.Clear();
        foreach (var record in records)
        {
            var schedule = string.Equals(record.ScheduleType, "daily", StringComparison.OrdinalIgnoreCase)
                ? $"Daily {record.DailyTime}"
                : $"Every {record.IntervalMinutes ?? 60} min";

            Automations.Add(new AutomationListItemViewModel
            {
                Id = record.Id,
                Name = record.Name,
                Enabled = record.Enabled,
                Schedule = schedule,
                NextRun = FormatDate(record.NextRunAt),
                LastRun = FormatDate(record.LastRunAt),
                LastStatus = record.LastStatus ?? string.Empty
            });
        }

        SelectedAutomation = Automations.FirstOrDefault();
    }

    private async Task LoadAutomationRunsAsync(string? automationId)
    {
        AutomationRuns.Clear();
        if (string.IsNullOrWhiteSpace(automationId))
        {
            return;
        }

        var runs = await _automationService.GetRunsAsync(automationId);
        foreach (var run in runs)
        {
            AutomationRuns.Add(new AutomationRunListItemViewModel
            {
                Id = run.Id,
                TriggerType = run.TriggerType,
                Status = run.Status,
                StartedAt = FormatDate(run.StartedAt),
                FinishedAt = FormatDate(run.FinishedAt)
            });
        }

        SelectedAutomationRun = AutomationRuns.FirstOrDefault();
    }

    private async Task NewAutomationAsync()
    {
        AutomationRequested?.Invoke(this, new AutomationEditorRequestedEventArgs(null));
        await Task.CompletedTask;
    }

    private async Task EditAutomationAsync()
    {
        if (SelectedAutomation is null)
        {
            return;
        }

        var records = await _automationService.GetAutomationsAsync();
        var existing = records.FirstOrDefault(x => x.Id == SelectedAutomation.Id);
        AutomationRequested?.Invoke(this, new AutomationEditorRequestedEventArgs(existing));
    }

    public event EventHandler<AutomationEditorRequestedEventArgs>? AutomationRequested;

    public async Task SaveAutomationFromDialogAsync(AutomationRecord? existing, AutomationUpsertRequest request)
    {
        if (existing is null)
        {
            await _automationService.CreateAutomationAsync(request);
            AutomationStatusMessage = "Automation created.";
        }
        else
        {
            await _automationService.UpdateAutomationAsync(existing.Id, request);
            AutomationStatusMessage = "Automation updated.";
        }

        await LoadAutomationsAsync();
    }

    private async Task DeleteAutomationAsync()
    {
        if (SelectedAutomation is null)
    public IReadOnlyList<DocumentListItemViewModel> GetSuggestedDocumentsForSelectedArtifact()
    {
        var selectedRunCompanyId = SelectedAgentRun?.CompanyId;
        if (!string.IsNullOrWhiteSpace(selectedRunCompanyId))
        {
            var sameCompany = Documents
                .Where(d => string.Equals(d.CompanyId, selectedRunCompanyId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sameCompany.Count > 0)
            {
                return sameCompany;
            }
        }

        return Documents.ToList();
    }

    public Task<DocumentRecord?> GetDocumentDetailsForEvidenceAsync(string documentId)
        => _documentImportService.GetDocumentDetailsAsync(documentId);

    public async Task CreateSnippetAndLinkToArtifactAsync(string artifactId, string documentId, string locator, string snippetText, string? companyId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            AgentStatusMessage = "Select an artifact before linking evidence.";
            return;
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            AgentStatusMessage = "Select a document to create evidence.";
            return;
        }

        if (string.IsNullOrWhiteSpace(locator) || string.IsNullOrWhiteSpace(snippetText))
        {
            AgentStatusMessage = "Select text and provide a locator before saving evidence.";
            return;
        }

        var detail = await _documentImportService.GetDocumentDetailsAsync(documentId);
        if (detail is null || string.IsNullOrWhiteSpace(detail.WorkspaceId))
        {
            AgentStatusMessage = "Unable to load document details for evidence creation.";
            return;
        }

        await _evidenceService.AddSnippetAndLinkToArtifactAsync(
            detail.WorkspaceId,
            artifactId,
            documentId,
            string.IsNullOrWhiteSpace(companyId) ? null : companyId,
            sourceId: null,
            locator,
            snippetText,
            createdBy: "user",
            relevanceScore: null);

        AgentStatusMessage = "Snippet created and linked to artifact.";
    public async Task RefreshArtifactEvidenceAsync()
    {
        ArtifactEvidenceLinks.Clear();

        if (SelectedRunArtifact is null)
        {
            ArtifactEvidenceStatusMessage = "Select an artifact to view linked evidence.";
            return;
        }

        var evidenceLinks = await _evidenceService.ListEvidenceLinksByArtifactAsync(SelectedRunArtifact.Id);
        foreach (var evidenceLink in evidenceLinks)
        {
            ArtifactEvidenceLinks.Add(new ArtifactEvidenceListItemViewModel
            {
                EvidenceLinkId = evidenceLink.Id,
                SnippetId = evidenceLink.SnippetId,
                DocumentId = evidenceLink.DocumentId,
                SnippetPreview = BuildSnippetPreview(evidenceLink.SnippetText, evidenceLink.Quote),
                Locator = string.IsNullOrWhiteSpace(evidenceLink.Locator) ? "—" : evidenceLink.Locator,
                DocumentTitle = string.IsNullOrWhiteSpace(evidenceLink.DocumentTitle) ? "(unknown)" : evidenceLink.DocumentTitle,
                CompanyName = string.IsNullOrWhiteSpace(evidenceLink.CompanyName) ? string.Empty : evidenceLink.CompanyName
            });
        }

        ArtifactEvidenceStatusMessage = ArtifactEvidenceLinks.Count == 0
            ? "No evidence linked yet."
            : $"{ArtifactEvidenceLinks.Count} evidence link(s).";
    }

    public async Task<IReadOnlyList<SnippetPickerListItemViewModel>> SearchSnippetsForLinkingAsync(string? companyId, string? documentId, string? query)
    {
        var snippets = await _evidenceService.SearchSnippetsAsync(companyId, documentId, query);
        return snippets
            .Select(static snippet => new SnippetPickerListItemViewModel
            {
                Id = snippet.Id,
                DocumentId = snippet.DocumentId,
                DocumentTitle = snippet.DocumentTitle,
                Locator = snippet.Locator,
                TextPreview = BuildPreview(snippet.Text)
            })
            .ToList();
    }

    public async Task LinkSnippetToSelectedArtifactAsync(string snippetId)
    {
        if (SelectedRunArtifact is null || string.IsNullOrWhiteSpace(snippetId))
        {
            return;
        }

        await _notificationService.MarkRead(SelectedNotification.NotificationId);
        SelectedNotification.IsRead = true;
        MarkNotificationReadCommand.RaiseCanExecuteChanged();
    }

    private void ShowToast(string message)
    {
        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();

        ToastMessage = message;
        IsToastVisible = true;

        _ = HideToastAsync(_toastCts.Token);
    }

    private async Task HideToastAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            IsToastVisible = false;
        }
        catch (TaskCanceledException)
        {
        }
        await _automationService.DeleteAutomationAsync(SelectedAutomation.Id);
        AutomationStatusMessage = "Automation deleted.";
        await LoadAutomationsAsync();
    }

    private async Task RunAutomationNowAsync()
    {
        if (SelectedAutomation is null)
        await _evidenceService.CreateEvidenceLinkAsync(SelectedRunArtifact.Id, snippetId, null, null, null, null);
        await RefreshArtifactEvidenceAsync();
        AgentStatusMessage = "Snippet linked to artifact.";
    }

    public async Task RemoveEvidenceLinkAsync(string evidenceLinkId)
    {
        if (string.IsNullOrWhiteSpace(evidenceLinkId))
        {
            return;
        }

        await _automationService.RunNowAsync(SelectedAutomation.Id);
        AutomationStatusMessage = "Automation run triggered.";
        await LoadAutomationsAsync();
        SelectedAutomation = Automations.FirstOrDefault(x => x.Id == SelectedAutomation?.Id) ?? Automations.FirstOrDefault();
    }

    private async Task ToggleAutomationEnabledAsync()
    {
        if (SelectedAutomation is null)
        await _evidenceService.DeleteEvidenceLinkAsync(evidenceLinkId);
        await RefreshArtifactEvidenceAsync();
        AgentStatusMessage = "Evidence link removed.";
    }

    public void OpenDocumentDetails(string? documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return;
        }

        var match = Documents.FirstOrDefault(d => string.Equals(d.Id, documentId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        await _automationService.SetAutomationEnabledAsync(SelectedAutomation.Id, !SelectedAutomation.Enabled);
        await LoadAutomationsAsync();
        SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Documents", StringComparison.OrdinalIgnoreCase));
        SelectedDocument = match;
        DocumentStatusMessage = $"Opened document: {match.Title}";
    }

    private void OpenSearchResult(SearchResultListItemViewModel? result)
    {
        if (result is null)
        {
            return;
        }

        if (string.Equals(result.ResultType, "note", StringComparison.OrdinalIgnoreCase))
        {
            SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Notes", StringComparison.OrdinalIgnoreCase));
            SelectedNote = Notes.FirstOrDefault(n => n.Id == result.EntityId) ?? AllNotes.FirstOrDefault(n => n.Id == result.EntityId);
            if (SelectedNote is not null)
            {
                NoteStatusMessage = $"Opened note match: {result.Title}";
            }

            return;
        }

        if (string.Equals(result.ResultType, "document", StringComparison.OrdinalIgnoreCase))
        {
            SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Documents", StringComparison.OrdinalIgnoreCase));
            SelectedDocument = Documents.FirstOrDefault(d => d.Id == result.EntityId);
            if (SelectedDocument is not null)
            {
                DocumentStatusMessage = $"Opened document match: {result.Title}";
                if (!string.IsNullOrWhiteSpace(result.Snippet))
                {
                    DetailsTextPreview = result.Snippet;
                }
            }

            return;
        }

        SearchStatusMessage = $"{result.ResultType} results are searchable, but this build does not yet have a dedicated detail view.";
    }

    private async Task LoadDataQualityReportAsync()
    {
        var report = await _dataQualityService.GetReportAsync();

        DataQualityDuplicateGroups.Clear();
        foreach (var duplicateGroup in report.Duplicates)
        {
            var docs = duplicateGroup.Documents.Select(d => new DataQualityDuplicateDocumentViewModel
            {
                Id = d.Id,
                Title = d.Title,
                ImportedAt = FormatDate(d.ImportedAt)
            }).ToList();

            DataQualityDuplicateGroups.Add(new DataQualityDuplicateGroupViewModel
            {
                ContentHash = duplicateGroup.ContentHash,
                DisplayLabel = $"{duplicateGroup.ContentHash[..Math.Min(12, duplicateGroup.ContentHash.Length)]}… ({docs.Count})",
                Documents = docs
            });
        }

        DataQualityUnlinkedDocuments.Clear();
        foreach (var item in report.UnlinkedDocuments)
        {
            DataQualityUnlinkedDocuments.Add(new DataQualityUnlinkedListItemViewModel { Id = item.Id, Title = item.Title, CreatedAt = FormatDate(item.CreatedAt) });
        }

        DataQualityUnlinkedNotes.Clear();
        foreach (var item in report.UnlinkedNotes)
        {
            DataQualityUnlinkedNotes.Add(new DataQualityUnlinkedListItemViewModel { Id = item.Id, Title = item.Title, CreatedAt = FormatDate(item.CreatedAt) });
        }

        DataQualityEvidenceGaps.Clear();
        foreach (var item in report.EvidenceGaps)
        {
            DataQualityEvidenceGaps.Add(new DataQualityArtifactGapViewModel { ArtifactId = item.ArtifactId, Title = item.Title, CreatedAt = FormatDate(item.CreatedAt) });
        }

        DataQualityMetricIssues.Clear();
        foreach (var item in report.MetricEvidenceIssues)
        {
            DataQualityMetricIssues.Add(new DataQualityMetricIssueViewModel { MetricId = item.MetricId, MetricKey = item.MetricKey, RecordedAt = FormatDate(item.RecordedAt) });
        }

        DataQualitySnippetIssues.Clear();
        foreach (var item in report.SnippetIssues)
        {
            var parent = !string.IsNullOrWhiteSpace(item.DocumentId) ? $"Doc: {item.DocumentId}" : (!string.IsNullOrWhiteSpace(item.SourceId) ? $"Source: {item.SourceId}" : "(missing parent)");
            DataQualitySnippetIssues.Add(new DataQualitySnippetIssueViewModel { SnippetId = item.SnippetId, Locator = item.Locator, ParentReference = parent });
        }

        DataQualityEnrichmentSuggestions.Clear();
        foreach (var item in report.EnrichmentSuggestions)
        {
            DataQualityEnrichmentSuggestions.Add(new DataQualityEnrichmentSuggestionViewModel
            {
                ItemType = item.ItemType,
                ItemId = item.ItemId,
                ItemTitle = item.ItemTitle,
                CompanyId = item.CompanyId,
                CompanyName = item.CompanyName,
                MatchedTerm = item.MatchedTerm,
                MatchReason = item.MatchReason,
                CreatedAt = FormatDate(item.CreatedAt)
            });
        }

        DataQualityStatusMessage = $"Duplicates: {DataQualityDuplicateGroups.Count}, unlinked docs: {DataQualityUnlinkedDocuments.Count}, unlinked notes: {DataQualityUnlinkedNotes.Count}, suggestions: {DataQualityEnrichmentSuggestions.Count}, evidence gaps: {DataQualityEvidenceGaps.Count}.";
    }

    private async Task LinkSelectedUnlinkedDocumentAsync()
    {
        if (SelectedUnlinkedDocument is null || SelectedQualityCompany is null)
        {
            return;
        }

        await _dataQualityService.LinkDocumentToCompanyAsync(SelectedUnlinkedDocument.Id, SelectedQualityCompany.Id);
        await LoadDocumentsAsync();
        await LoadDataQualityReportAsync();
    }

    private async Task LinkSelectedUnlinkedNoteAsync()
    {
        if (SelectedUnlinkedNote is null || SelectedQualityCompany is null)
        {
            return;
        }

        await _dataQualityService.LinkNoteToCompanyAsync(SelectedUnlinkedNote.Id, SelectedQualityCompany.Id);
        await LoadNotesAsync();
        await LoadDataQualityReportAsync();
    }

    private async Task ArchiveDuplicateGroupAsync()
    {
        if (SelectedDuplicateGroup is null || SelectedDuplicateKeepDocument is null)
        {
            return;
        }

        await _dataQualityService.ArchiveDuplicateDocumentsAsync(SelectedDuplicateGroup.ContentHash, SelectedDuplicateKeepDocument.Id);
        await LoadDocumentsAsync();
        await LoadDataQualityReportAsync();
    }

    private async Task ApplySelectedEnrichmentSuggestionAsync()
    {
        if (SelectedEnrichmentSuggestion is null)
        {
            return;
        }

        await _dataQualityService.ApplyEnrichmentSuggestionAsync(
            SelectedEnrichmentSuggestion.ItemType,
            SelectedEnrichmentSuggestion.ItemId,
            SelectedEnrichmentSuggestion.CompanyId);

        await LoadDocumentsAsync();
        await LoadNotesAsync();
        await LoadDataQualityReportAsync();
    }

    private void OpenSelectedEvidenceGap()
    {
        if (SelectedEvidenceGap is null)
        {
            return;
        }

        SelectedItem = NavigationItems.First(i => string.Equals(i.Title, "Agents", StringComparison.OrdinalIgnoreCase));
        SelectedRunArtifact = RunArtifacts.FirstOrDefault(a => a.Id == SelectedEvidenceGap.ArtifactId);
        AgentStatusMessage = $"Evidence gap artifact selected: {SelectedEvidenceGap.Title}";
    }


    public async Task RefreshAfterWeeklyReviewAsync()
    {
        await LoadNotesAsync();
        await LoadDashboardAsync();
        NoteStatusMessage = "Weekly review created.";
    }

    public async Task RefreshAfterInvestmentMemoAsync(string companyId)
    {
        await LoadNotesAsync();
        await LoadCompanyHubAsync(companyId);
        InvestmentMemoStatusMessage = "Investment memo created.";
    }

    public async Task RefreshAfterQuarterlyReviewAsync(string companyId)
    {
        await LoadNotesAsync();
        await LoadCompanyHubAsync(companyId);
        NoteStatusMessage = "Quarterly review created.";
    }

    public async Task<string> ImportPricesCsvAsync(string csvFilePath, string? dateColumn = null, string? closeColumn = null)
    {
        if (SelectedHubCompany is null)
        {
            return "Select a company first.";
        }

        var result = await _companyService.ImportCompanyDailyPricesCsvAsync(SelectedHubCompany.Id, csvFilePath, dateColumn, closeColumn);
        await LoadCompanyHubAsync(SelectedHubCompany.Id);
        return $"Imported {result.InsertedOrUpdatedCount} rows ({result.SkippedCount} skipped).";
    }


    private PortfolioDashboardSnapshot _dashboardSnapshot = new();

    private async Task LoadDashboardAsync()
    {
        DashboardRows.Clear();

        var workspaceId = SelectedSearchWorkspace?.Id ?? WorkspaceOptions.FirstOrDefault()?.Id ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            var workspaces = await _searchService.GetWorkspacesAsync();
            workspaceId = workspaces.FirstOrDefault()?.Id ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            DashboardStatusMessage = "No workspace available.";
            return;
        }

        var companies = await _companyService.GetCompaniesAsync();
        if (companies.Count == 0)
        {
            DashboardStatusMessage = "No companies available.";
            _dashboardSnapshot = new PortfolioDashboardSnapshot();
            ApplyDashboardFilters();
            return;
        }

        var inputRows = new List<PortfolioDashboardInputRow>();
        foreach (var company in companies)
        {
            var latestPrice = await _companyService.GetLatestCompanyPriceAsync(company.CompanyId);
            var stats = await _positionAnalyticsService.GetPositionStatsAsync(workspaceId, company.CompanyId, latestPrice: latestPrice?.Close);
            if (stats.NetQuantity == 0d && Math.Abs(stats.RealizedPnl) < 0.000001d)
            {
                continue;
            }

            inputRows.Add(new PortfolioDashboardInputRow
            {
                CompanyId = company.CompanyId,
                CompanyName = company.Name,
                Currency = latestPrice?.Currency ?? company.Currency ?? "N/A",
                PositionStats = stats,
                LastPrice = latestPrice?.Close
            });
        }

        _dashboardSnapshot = PortfolioDashboardCalculator.Build(inputRows);
        DashboardCurrencyOptions.Clear();
        DashboardCurrencyOptions.Add("All");
        foreach (var currency in _dashboardSnapshot.Rows.Select(r => r.Currency).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c))
        {
            DashboardCurrencyOptions.Add(currency);
        }

        DashboardStatusMessage = _dashboardSnapshot.Rows.Count == 0
            ? "No portfolio positions available."
            : $"Loaded {_dashboardSnapshot.Rows.Count} positions.";

        ApplyDashboardFilters();
    }

    private void ApplyDashboardFilters()
    {
        DashboardRows.Clear();

        IEnumerable<PortfolioDashboardRow> rows = _dashboardSnapshot.Rows;
        rows = DashboardPositionFilter switch
        {
            "Closed" => rows.Where(r => Math.Abs(r.Quantity) < 0.000001d),
            "Open" => rows.Where(r => Math.Abs(r.Quantity) >= 0.000001d),
            _ => rows
        };

        if (!string.Equals(DashboardCurrencyFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(r => string.Equals(r.Currency, DashboardCurrencyFilter, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = rows.ToList();
        foreach (var row in filtered)
        {
            DashboardRows.Add(new PortfolioAllocationRowViewModel
            {
                Company = row.CompanyName,
                Currency = row.Currency,
                Quantity = row.Quantity,
                AverageCost = row.AverageCost,
                LastPrice = row.LastPrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "N/A",
                MarketValue = row.MarketValue?.ToString("0.00", CultureInfo.InvariantCulture) ?? "N/A",
                Pnl = row.UnrealizedPnl?.ToString("0.00", CultureInfo.InvariantCulture) ?? "N/A",
                Allocation = $"{row.AllocationPercent:0.00}%",
                IsOpen = Math.Abs(row.Quantity) >= 0.000001d
            });
        }

        DashboardTotalInvested = filtered.Sum(r => r.CostBasis).ToString("0.00", CultureInfo.InvariantCulture);
        DashboardTotalMarketValue = filtered.All(r => r.MarketValue.HasValue) ? filtered.Sum(r => r.MarketValue ?? 0d).ToString("0.00", CultureInfo.InvariantCulture) : "N/A";
        DashboardTotalUnrealizedPnl = filtered.All(r => r.UnrealizedPnl.HasValue) ? filtered.Sum(r => r.UnrealizedPnl ?? 0d).ToString("0.00", CultureInfo.InvariantCulture) : "N/A";
        DashboardTotalRealizedPnl = filtered.Sum(r => r.RealizedPnl).ToString("0.00", CultureInfo.InvariantCulture);
        DashboardBiggestWinner = filtered.Where(r => r.UnrealizedPnl.HasValue).OrderByDescending(r => r.UnrealizedPnl).FirstOrDefault()?.CompanyName ?? "N/A";
        DashboardBiggestLoser = filtered.Where(r => r.UnrealizedPnl.HasValue).OrderBy(r => r.UnrealizedPnl).FirstOrDefault()?.CompanyName ?? "N/A";
    }

    private static string StripHighlight(string value) => value.Replace("<mark>", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("</mark>", string.Empty, StringComparison.OrdinalIgnoreCase);

    private bool IsSelected(string title) => string.Equals(SelectedItem.Title, title, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record AutomationEditorRequestedEventArgs(AutomationRecord? ExistingAutomation);

    private static string FormatDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return "";
        }

        return DateTime.TryParse(dateString, out var parsed)
            ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : dateString;
    }

    private static string BuildSnippetPreview(string? snippetText, string? quote)
    {
        if (!string.IsNullOrWhiteSpace(quote))
        {
            return BuildPreview(quote);
        }

        return BuildPreview(snippetText);
    }

    private static string BuildPreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(no snippet text)";
        }

        var trimmed = text.Trim();
        return trimmed.Length <= 200 ? trimmed : $"{trimmed[..200]}…";
    }

    public async Task<string> FetchAnnouncementsForSelectedCompanyAsync(int days, string? manualUrls)
    {
        if (SelectedHubCompany is null)
        {
            return "Select a company first.";
        }

        var workspaceId = SelectedSearchWorkspace?.Id ?? WorkspaceOptions.FirstOrDefault()?.Id ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return "No workspace selected.";
        }

        var connectorSettings = new Dictionary<string, string>
        {
            ["days"] = days.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(manualUrls))
        {
            connectorSettings["manual_urls"] = manualUrls;
        }

        var context = new ConnectorContext
        {
            WorkspaceId = workspaceId,
            CompanyId = SelectedHubCompany.Id,
            HttpClient = _connectorHttpClient,
            Settings = connectorSettings,
            Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
        };

        var result = await _connectorRegistry.RunAsync("announcements-fetch", context);
        var summary = $"Added {result.SourcesCreated}, skipped {result.SourcesUpdated}, errors {result.Errors.Count}";
        await _notificationService.AddNotification(result.Errors.Count == 0 ? "info" : "warn", "Announcements fetch finished", summary);

        await LoadCompanyHubAsync(SelectedHubCompany.Id);
        await LoadDocumentsAsync();
        await LoadNotificationsAsync();
        return summary;
    }

    private void LoadConnectors()
    {
        Connectors.Clear();
        foreach (var connector in _connectorRegistry.GetConnectors())
        {
            Connectors.Add(new ConnectorListItemViewModel { Id = connector.Id, DisplayName = connector.DisplayName });
        }

        SelectedConnector = Connectors.FirstOrDefault();
    }

    private async Task RunSelectedConnectorAsync()
    {
        if (SelectedConnector is null)
        {
            return;
        }

        var settings = await _appSettingsService.GetSettingsAsync();
        var workspaceId = SelectedSearchWorkspace?.Id ?? WorkspaceOptions.FirstOrDefault()?.Id ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            ConnectorRunResult = "No workspace selected.";
            return;
        }

        var connectorSettings = new Dictionary<string, string>();
        if (string.Equals(SelectedConnector.Id, "ose-directory-import", StringComparison.OrdinalIgnoreCase))
        {
            connectorSettings["csv_path"] = ConnectorUrl;
        }
        else
        {
            connectorSettings["url"] = ConnectorUrl;
        }

        var context = new ConnectorContext
        {
            WorkspaceId = workspaceId,
            CompanyId = null,
            HttpClient = _connectorHttpClient,
            Settings = connectorSettings,
            Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
        };

        var result = await _connectorRegistry.RunAsync(SelectedConnector.Id, context);
        ConnectorRunResult = $"Sources +{result.SourcesCreated}, Documents +{result.DocumentsCreated}, Errors: {result.Errors.Count}";

        await _notificationService.AddNotification(result.Errors.Count == 0 ? "info" : "warn", "Connector run finished", ConnectorRunResult);
        await LoadDocumentsAsync();
        await LoadNotificationsAsync();
    }

}
