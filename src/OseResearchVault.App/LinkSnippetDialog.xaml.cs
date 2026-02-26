using System.Windows;
using OseResearchVault.App.ViewModels;

namespace OseResearchVault.App;

public partial class LinkSnippetDialog : Window
{
    private readonly Func<string?, string?, string?, Task<IReadOnlyList<SnippetPickerListItemViewModel>>> _search;

    public LinkSnippetDialog(
        IReadOnlyList<CompanyOptionViewModel> companyOptions,
        IReadOnlyList<DocumentListItemViewModel> documentOptions,
        string? defaultCompanyId,
        Func<string?, string?, string?, Task<IReadOnlyList<SnippetPickerListItemViewModel>>> search)
    {
        _search = search;
        InitializeComponent();

        CompanyCombo.ItemsSource = companyOptions;
        CompanyCombo.SelectedValue = string.IsNullOrWhiteSpace(defaultCompanyId) ? string.Empty : defaultCompanyId;

        var docs = new List<DocumentListItemViewModel>
        {
            new() { Id = string.Empty, Title = "All documents" }
        };
        docs.AddRange(documentOptions.OrderBy(d => d.Title));
        DocumentCombo.ItemsSource = docs;
        DocumentCombo.SelectedValue = string.Empty;

        Loaded += async (_, _) => await RefreshResultsAsync();
    }

    public SnippetPickerListItemViewModel? SelectedSnippet => ResultsGrid.SelectedItem as SnippetPickerListItemViewModel;

    private async void Search_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshResultsAsync();
    }

    private async Task RefreshResultsAsync()
    {
        var items = await _search(CompanyId, DocumentId, SearchTextBox.Text);
        ResultsGrid.ItemsSource = items;
        if (items.Count > 0)
        {
            ResultsGrid.SelectedIndex = 0;
        }
    }

    private void Link_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedSnippet is null)
        {
            MessageBox.Show(this, "Select a snippet to link.", "Link Snippet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private string? CompanyId
    {
        get
        {
            var selected = CompanyCombo.SelectedValue as string;
            return string.IsNullOrWhiteSpace(selected) ? null : selected;
        }
    }

    private string? DocumentId
    {
        get
        {
            var selected = DocumentCombo.SelectedValue as string;
            return string.IsNullOrWhiteSpace(selected) ? null : selected;
        }
    }
}
