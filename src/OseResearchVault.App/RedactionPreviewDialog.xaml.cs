using System.Windows;

namespace OseResearchVault.App;

public partial class RedactionPreviewDialog : Window
{
    public RedactionPreviewDialog(string originalText, string redactedText, int hitCount)
    {
        InitializeComponent();
        SummaryText.Text = $"Redaction hits: {hitCount}";
        OriginalTextBox.Text = originalText;
        RedactedTextBox.Text = redactedText;
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
