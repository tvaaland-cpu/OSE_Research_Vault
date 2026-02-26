using System.Globalization;
using System.Windows;
using OseResearchVault.App.ViewModels;

namespace OseResearchVault.App;

public partial class CreateMetricDialog : Window
{
    public CreateMetricDialog(IReadOnlyList<CompanyOptionViewModel> companies, string? snippetCompanyId, string documentTitle, string locator, string snippetText, string? defaultCurrency)
    {
        InitializeComponent();
        CompanyCombo.ItemsSource = companies;
        CompanyCombo.SelectedValue = snippetCompanyId;
        CompanyCombo.IsEnabled = string.IsNullOrWhiteSpace(snippetCompanyId);

        DocumentTitleText.Text = documentTitle;
        EvidenceText.Text = $"Locator: {locator}{Environment.NewLine}{Environment.NewLine}{snippetText}";
        CurrencyText.Text = defaultCurrency ?? string.Empty;
    }

    public string? CompanyId => CompanyCombo.SelectedValue as string;
    public string MetricName => MetricNameText.Text.Trim();
    public string Period => PeriodText.Text.Trim();
    public string? Unit => UnitCombo.Text.Trim();
    public string? Currency => string.IsNullOrWhiteSpace(CurrencyText.Text) ? null : CurrencyText.Text.Trim();
    public double Value { get; private set; }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CompanyId))
        {
            MessageBox.Show(this, "Company is required.", "Create Metric", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(MetricName))
        {
            MessageBox.Show(this, "Metric name is required.", "Create Metric", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Period))
        {
            MessageBox.Show(this, "Period is required.", "Create Metric", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(ValueText.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !double.TryParse(ValueText.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            MessageBox.Show(this, "Value must be numeric.", "Create Metric", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Value = value;
        DialogResult = true;
    }
}
