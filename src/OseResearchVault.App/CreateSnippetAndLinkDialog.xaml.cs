using System.Windows;
using System.Windows.Controls;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class CreateSnippetAndLinkDialog : Window
{
    private readonly Func<string, Task<DocumentRecord?>> _loadDocumentDetailsAsync;

    public CreateSnippetAndLinkDialog(
        IReadOnlyList<DocumentChoice> documents,
        Func<string, Task<DocumentRecord?>> loadDocumentDetailsAsync)
    {
        InitializeComponent();
        _loadDocumentDetailsAsync = loadDocumentDetailsAsync;
        DocumentSelector.ItemsSource = documents;
        DocumentSelector.SelectedIndex = documents.Count > 0 ? 0 : -1;
    }

    public string SelectedDocumentId => (DocumentSelector.SelectedItem as DocumentChoice)?.Id ?? string.Empty;
    public string? SelectedCompanyId => (DocumentSelector.SelectedItem as DocumentChoice)?.CompanyId;
    public string Locator => LocatorText.Text.Trim();
    public string Snippet => SnippetText.Text.Trim();

    private async void DocumentSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await RefreshSelectedDocumentPreviewAsync();
    }

    private void PreviewText_OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (PreviewText.SelectionLength <= 0)
        {
            return;
        }

        var start = PreviewText.SelectionStart;
        var end = start + PreviewText.SelectionLength;
        LocatorText.Text = $"sel=offset:{start}-{end}";
        SnippetText.Text = PreviewText.SelectedText;
    }

    private async Task RefreshSelectedDocumentPreviewAsync()
    {
        var documentId = SelectedDocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            PreviewText.Text = string.Empty;
            LocatorText.Text = string.Empty;
            SnippetText.Text = string.Empty;
            return;
        }

        var detail = await _loadDocumentDetailsAsync(documentId);
        var extractedText = string.IsNullOrWhiteSpace(detail?.ExtractedText)
            ? "No extracted text available for this document."
            : detail.ExtractedText;

        PreviewText.Text = extractedText;
        PreviewText.Select(0, 0);
        LocatorText.Text = "";
        SnippetText.Text = "";
    }

    private void CreateAndLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedDocumentId))
        {
            MessageBox.Show(this, "Document selection is required.", "Create Snippet & Link", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Locator))
        {
            MessageBox.Show(this, "Locator is required.", "Create Snippet & Link", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Snippet))
        {
            MessageBox.Show(this, "Snippet text is required.", "Create Snippet & Link", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    public sealed class DocumentChoice
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? CompanyId { get; init; }
    }
}
