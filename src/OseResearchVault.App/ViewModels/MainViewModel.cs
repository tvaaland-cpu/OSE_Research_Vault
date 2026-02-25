using System.Collections.ObjectModel;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IDocumentImportService _documentImportService;
    private NavigationItem _selectedItem;
    private DocumentListItemViewModel? _selectedDocument;
    private string _documentStatusMessage = "Drop files to import into the vault.";
    private string _detailsSummary = "Select a document to view metadata.";
    private string _detailsTextPreview = string.Empty;
    private bool _isBusy;

    public MainViewModel(IDocumentImportService documentImportService)
    {
        _documentImportService = documentImportService;

        NavigationItems =
        [
            new NavigationItem("Dashboard"),
            new NavigationItem("Companies"),
            new NavigationItem("Watchlist"),
            new NavigationItem("Documents"),
            new NavigationItem("Notes"),
            new NavigationItem("Agents"),
            new NavigationItem("Search"),
            new NavigationItem("Settings")
        ];

        Documents = [];
        RefreshDocumentsCommand = new RelayCommand(() => _ = LoadDocumentsAsync(), () => !_isBusy);

        _selectedItem = NavigationItems[0];
        _ = LoadDocumentsAsync();
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public ObservableCollection<DocumentListItemViewModel> Documents { get; }
    public RelayCommand RefreshDocumentsCommand { get; }

    public NavigationItem SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(IsDocumentsSelected));
            }
        }
    }

    public bool IsDocumentsSelected => string.Equals(SelectedItem.Title, "Documents", StringComparison.OrdinalIgnoreCase);

    public DocumentListItemViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                _ = LoadDocumentDetailsAsync(value?.Id);
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

    public async Task ImportDocumentsAsync(IEnumerable<string> filePaths)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        RefreshDocumentsCommand.RaiseCanExecuteChanged();

        try
        {
            var fileList = filePaths.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (fileList.Count == 0)
            {
                return;
            }

            var results = await _documentImportService.ImportFilesAsync(fileList);
            var successCount = results.Count(static r => r.Succeeded);
            var failures = results.Where(static r => !r.Succeeded).ToList();

            if (failures.Count == 0)
            {
                DocumentStatusMessage = $"Imported {successCount} document(s).";
            }
            else
            {
                var errorText = string.Join(Environment.NewLine, failures.Select(f => $"• {Path.GetFileName(f.FilePath)}: {f.ErrorMessage}"));
                DocumentStatusMessage = $"Imported {successCount} document(s), {failures.Count} failed:{Environment.NewLine}{errorText}";
            }

            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            DocumentStatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            _isBusy = false;
            RefreshDocumentsCommand.RaiseCanExecuteChanged();
        }
    }

    public async Task LoadDocumentsAsync()
    {
        try
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
                    Company = doc.CompanyName ?? "",
                    PublishedDate = FormatDate(doc.PublishedAt),
                    ImportedDate = FormatDate(doc.ImportedAt)
                });
            }
        }
        catch (Exception ex)
        {
            DocumentStatusMessage = $"Could not load documents: {ex.Message}";
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
