using System.Windows;

namespace OseResearchVault.App;

public partial class QuarterlyReviewPeriodDialog : Window
{
    public QuarterlyReviewPeriodDialog(string defaultPeriod)
    {
        InitializeComponent();
        PeriodLabel = defaultPeriod;
        DataContext = this;
    }

    public string PeriodLabel { get; set; }

    private void Generate_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PeriodLabel))
        {
            MessageBox.Show(this, "Enter a period label in the format YYYYQ#.", "Quarterly Review", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
