using System.Collections.Generic;
using System.Linq;
using System.Windows;
using OseResearchVault.App.ViewModels;

namespace OseResearchVault.App;

public partial class QuickCaptureDialog : Window
{
    public QuickCaptureDialog(IEnumerable<CompanyOptionViewModel> companyOptions, CompanyOptionViewModel? selectedCompany)
    {
        InitializeComponent();

        var options = companyOptions.ToList();
        if (!options.Any(c => string.IsNullOrWhiteSpace(c.Id)))
        {
            options.Insert(0, new CompanyOptionViewModel { Id = string.Empty, DisplayName = "(No company)" });
        }

        CompanyComboBox.ItemsSource = options;
        CompanyComboBox.SelectedItem = options.FirstOrDefault(c => string.Equals(c.Id, selectedCompany?.Id, System.StringComparison.OrdinalIgnoreCase)) ?? options.FirstOrDefault();
    }

    public string Url => UrlTextBox.Text.Trim();
    public string TextContent => TextTextBox.Text;
    public CompanyOptionViewModel? SelectedCompany => CompanyComboBox.SelectedItem as CompanyOptionViewModel;
    public QuickCaptureMode? CaptureMode { get; private set; }

    private void CaptureUrl_OnClick(object sender, RoutedEventArgs e)
    {
        CaptureMode = QuickCaptureMode.Url;
        DialogResult = true;
    }

    private void CaptureText_OnClick(object sender, RoutedEventArgs e)
    {
        CaptureMode = QuickCaptureMode.Text;
        DialogResult = true;
    }

    private void OpenAiImport_OnClick(object sender, RoutedEventArgs e)
    {
        CaptureMode = QuickCaptureMode.AiImport;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public enum QuickCaptureMode
{
    Url,
    Text,
    AiImport
}
