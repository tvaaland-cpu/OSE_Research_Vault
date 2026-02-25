using System.Collections.ObjectModel;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IDocumentImportService _documentImportService;
    private readonly ICompanyService _companyService;
    private readonly INoteService _noteService;
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

    public MainViewModel(IDocumentImportService documentImportService, ICompanyService companyService, INoteService noteService)
    {
        _documentImportService = documentImportService;
        _companyService = companyService;
        _noteService = noteService;

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

        RefreshDocumentsCommand = new RelayCommand(() => _ = LoadDocumentsAsync());
        SaveDocumentCompanyCommand = new RelayCommand(() => _ = SaveSelectedDocumentCompanyAsync(), () => SelectedDocument is not null);
        SaveCompanyCommand = new RelayCommand(() => _ = SaveCompanyAsync(), () => !string.IsNullOrWhiteSpace(CompanyName));
        DeleteCompanyCommand = new RelayCommand(() => _ = DeleteCompanyAsync(), () => SelectedCompany is not null);
        AddTagCommand = new RelayCommand(() => _ = AddTagAsync(), () => !string.IsNullOrWhiteSpace(NewTagName));
        NewCompanyCommand = new RelayCommand(ClearCompanyForm);
        SaveNoteCommand = new RelayCommand(() => _ = SaveNoteAsync(), () => !string.IsNullOrWhiteSpace(NoteTitle));
        DeleteNoteCommand = new RelayCommand(() => _ = DeleteNoteAsync(), () => SelectedNote is not null);
        NewNoteCommand = new RelayCommand(ClearNoteForm);

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
    public IReadOnlyList<string> NoteTypes { get; }
    public IReadOnlyList<string> NoteFilterTypes { get; }
    public RelayCommand RefreshDocumentsCommand { get; }
    public RelayCommand SaveDocumentCompanyCommand { get; }
    public RelayCommand SaveCompanyCommand { get; }
    public RelayCommand DeleteCompanyCommand { get; }
    public RelayCommand AddTagCommand { get; }
    public RelayCommand NewCompanyCommand { get; }
    public RelayCommand SaveNoteCommand { get; }
    public RelayCommand DeleteNoteCommand { get; }
    public RelayCommand NewNoteCommand { get; }

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
            }
        }
    }

    public bool IsDocumentsSelected => IsSelected("Documents");
    public bool IsCompaniesSelected => IsSelected("Companies");
    public bool IsNotesSelected => IsSelected("Notes");
    public bool IsCompanyHubSelected => IsSelected("Company Hub");

    public DocumentListItemViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                SaveDocumentCompanyCommand.RaiseCanExecuteChanged();
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
    }

    private async Task InitializeAsync()
    {
        await LoadCompaniesAndTagsAsync();
        await LoadDocumentsAsync();
        await LoadNotesAsync();
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
            return;
        }

        var detail = await _documentImportService.GetDocumentDetailsAsync(documentId);
        if (detail is null)
        {
            DetailsSummary = "Document not found.";
            DetailsTextPreview = string.Empty;
            return;
        }

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
    }

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
