using System.Windows;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class AiImportDialog : Window
{
    public AiImportDialog()
    {
        InitializeComponent();
    }

    public AiImportRequest? Request { get; private set; }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ModelTextBox.Text) ||
            string.IsNullOrWhiteSpace(PromptTextBox.Text) ||
            string.IsNullOrWhiteSpace(ResponseTextBox.Text))
        {
            MessageBox.Show(this, "Model, prompt, and response are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Request = new AiImportRequest
        {
            Model = ModelTextBox.Text,
            Prompt = PromptTextBox.Text,
            Response = ResponseTextBox.Text,
            Sources = string.IsNullOrWhiteSpace(SourcesTextBox.Text) ? null : SourcesTextBox.Text
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
