using System.Collections.ObjectModel;
using System.Text.Json;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IDocumentImportService _documentImportService;
    private readonly ICompanyService _companyService;
    private readonly INoteService _noteService;
    private readonly IEvidenceService _evidenceService;
    private readonly ISearchService _searchService;
    private readonly IAgentService _agentService;
    private readonly INotificationService _notificationService;
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
    private string _selectedDocumentWorkspaceId = string.Empty;
    private NotificationListItemViewModel? _selectedNotification;
    private string _toastMessage = string.Empty;
    private bool _isToastVisible;
    private CancellationTokenSource? _toastCts;

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService, IEvidenceService evidenceService, ISearchService searchService, IAgentService agentService, INotificationService notificationService)
    {
        _documentImportService = documentImportService;
        _companyService = companyService;
        _noteService = noteService;
        _evidenceService = evidenceService;
        _searchService = searchService;
        _agentService = agentService;
        _notificationService = notificationService;

        NavigationItems =
        [
            new NavigationItem("Dashboard"),
            new NavigationItem("Companies"),
            new NavigationItem("Company Hub"),
            new NavigationItem("Watchlist"),
            new NavigationItem("Documents"),
            new NavigationItem("Notes"),
            new NavigationItem("Agents"),
            new NavigationItem("Search"),
            new NavigationItem("Inbox"),
            new NavigationItem("Settings")
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
        HubAgentRuns = [];
        SearchResults = [];
        WorkspaceOptions = [];
        AgentTemplates = [];
        AgentRuns = [];
        RunSelectableDocuments = [];
        RunArtifacts = [];
        DocumentSnippets = [];
        Notifications = [];
        SearchTypeOptions = ["All", "Notes", "Documents", "Snippets", "Artifacts"];

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
        SaveAgentTemplateCommand = new RelayCommand(() => _ = SaveAgentTemplateAsync(), () => !string.IsNullOrWhiteSpace(AgentName));
        NewAgentTemplateCommand = new RelayCommand(ClearAgentTemplateForm);
        RunAgentCommand = new RelayCommand(() => _ = RunAgentAsync(), () => SelectedAgentTemplate is not null);
        SaveArtifactCommand = new RelayCommand(() => _ = SaveArtifactAsync(), () => SelectedRunArtifact is not null);
        RefreshInboxCommand = new RelayCommand(() => _ = LoadNotificationsAsync());
        MarkNotificationReadCommand = new RelayCommand(() => _ = MarkNotificationReadAsync(), () => SelectedNotification is not null && !SelectedNotification.IsRead);

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
    public ObservableCollection<string> HubMetrics { get; }
    public ObservableCollection<string> HubAgentRuns { get; }
    public ObservableCollection<SearchResultListItemViewModel> SearchResults { get; }
    public ObservableCollection<WorkspaceOptionViewModel> WorkspaceOptions { get; }
    public ObservableCollection<AgentTemplateListItemViewModel> AgentTemplates { get; }
    public ObservableCollection<AgentRunListItemViewModel> AgentRuns { get; }
    public ObservableCollection<DocumentListItemViewModel> RunSelectableDocuments { get; }
    public ObservableCollection<ArtifactListItemViewModel> RunArtifacts { get; }
    public ObservableCollection<DocumentSnippetListItemViewModel> DocumentSnippets { get; }
    public ObservableCollection<NotificationListItemViewModel> Notifications { get; }
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
    public RelayCommand SaveAgentTemplateCommand { get; }
    public RelayCommand NewAgentTemplateCommand { get; }
    public RelayCommand RunAgentCommand { get; }
    public RelayCommand SaveArtifactCommand { get; }
    public RelayCommand RefreshInboxCommand { get; }
    public RelayCommand MarkNotificationReadCommand { get; }

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
                OnPropertyChanged(nameof(IsInboxSelected));
            }
        }
    }

    public bool IsDocumentsSelected => IsSelected("Documents");
    public bool IsCompaniesSelected => IsSelected("Companies");
    public bool IsNotesSelected => IsSelected("Notes");
    public bool IsCompanyHubSelected => IsSelected("Company Hub");
    public bool IsSearchSelected => IsSelected("Search");
    public bool IsAgentsSelected => IsSelected("Agents");
    public bool IsInboxSelected => IsSelected("Inbox");

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
                _ = LoadCompanyHubAsync(value?.Id);
            }
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
    public string AgentStatusMessage { get => _agentStatusMessage; set => SetProperty(ref _agentStatusMessage, value); }
    public string RunInputSummary { get => _runInputSummary; set => SetProperty(ref _runInputSummary, value); }
    public string RunToolCallsSummary { get => _runToolCallsSummary; set => SetProperty(ref _runToolCallsSummary, value); }
    public bool CanCreateSnippet => SelectedDocument is not null;

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
        await LoadAgentsAsync();
        await LoadAgentRunsAsync();
        await LoadNotificationsAsync();
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

        AvailableTags.Clear();
        foreach (var tag in tags)
        {
            AvailableTags.Add(new TagSelectionViewModel { Id = tag.Id, Name = tag.Name });
        }

        SelectedHubCompany ??= CompanyOptions.FirstOrDefault();
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

    private async Task LoadCompanyHubAsync(string? companyId)
    {
        HubDocuments.Clear();
        HubNotes.Clear();
        HubEvents.Clear();
        HubMetrics.Clear();
        HubAgentRuns.Clear();

        if (string.IsNullOrWhiteSpace(companyId))
        {
            return;
        }

        var docs = await _companyService.GetCompanyDocumentsAsync(companyId);
        var notes = await _companyService.GetCompanyNotesAsync(companyId);
        var eventsList = await _companyService.GetCompanyEventsAsync(companyId);
        var metrics = await _companyService.GetCompanyMetricsAsync(companyId);
        var runs = await _companyService.GetCompanyAgentRunsAsync(companyId);

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
            HubMetrics.Add(item);
        }

        foreach (var item in runs)
        {
            HubAgentRuns.Add(item);
        }
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
                Locator = snippet.Locator,
                Text = snippet.Text,
                CreatedAt = FormatDate(snippet.CreatedAt)
            });
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
        if (SelectedAgentTemplate is null)
        {
            return;
        }

        var selectedDocIds = RunSelectableDocuments
            .Where(d => d.IsSelected)
            .Select(d => d.Id)
            .Distinct()
            .ToList();

        var runId = await _agentService.CreateRunAsync(new AgentRunRequest
        {
            AgentId = SelectedAgentTemplate.Id,
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
    }

    private async Task LoadRunNotebookAsync(AgentRunListItemViewModel? run)
    {
        RunArtifacts.Clear();
        if (run is null)
        {
            RunInputSummary = "Select a run to view notebook details.";
            return;
        }

        var selectedDocIds = JsonSerializer.Deserialize<List<string>>(run.SelectedDocumentIdsJson) ?? [];
        RunInputSummary = $"Query: {run.Query}{Environment.NewLine}Selected docs: {(selectedDocIds.Count == 0 ? "(none)" : string.Join(", ", selectedDocIds))}";
        RunToolCallsSummary = "No tool calls yet (MVP).";

        var artifacts = await _agentService.GetArtifactsAsync(run.Id);
        foreach (var artifact in artifacts)
        {
            RunArtifacts.Add(new ArtifactListItemViewModel { Id = artifact.Id, Title = artifact.Title, Content = artifact.Content });
        }

        SelectedRunArtifact = RunArtifacts.FirstOrDefault();
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

    private static string StripHighlight(string value) => value.Replace("<mark>", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("</mark>", string.Empty, StringComparison.OrdinalIgnoreCase);

    private bool IsSelected(string title) => string.Equals(SelectedItem.Title, title, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
}
