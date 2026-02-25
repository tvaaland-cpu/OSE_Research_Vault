using System.Windows;
using OseResearchVault.App.ViewModels;

namespace OseResearchVault.App;

public partial class CreateSnippetDialog : Window
{
    public CreateSnippetDialog(string documentTitle, IReadOnlyList<CompanyOptionViewModel> companies, string? selectedCompanyId, string defaultLocator, string selectedText)
    {
        InitializeComponent();
        DocumentTitleText.Text = documentTitle;
        CompanyCombo.ItemsSource = companies;
        CompanyCombo.SelectedValue = string.IsNullOrWhiteSpace(selectedCompanyId) ? string.Empty : selectedCompanyId;
        LocatorText.Text = defaultLocator;
        SnippetText.Text = selectedText;
    }

    public string Locator => LocatorText.Text.Trim();
    public string SnippetTextValue => SnippetText.Text.Trim();
    public string? CompanyId
    {
        get
        {
            var selected = CompanyCombo.SelectedValue as string;
            return string.IsNullOrWhiteSpace(selected) ? null : selected;
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Locator))
        {
            MessageBox.Show(this, "Locator is required.", "Create Snippet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SnippetTextValue))
        {
            MessageBox.Show(this, "Snippet text is required.", "Create Snippet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
