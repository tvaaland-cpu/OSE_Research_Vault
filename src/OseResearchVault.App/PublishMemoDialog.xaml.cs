using System.Windows;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class PublishMemoDialog : Window
{
    private readonly IReadOnlyList<ExportProfileRecord> _profiles;

    public PublishMemoDialog(IReadOnlyList<ExportProfileRecord> profiles, bool pdfSupported)
    {
        InitializeComponent();
        _profiles = profiles;
        ProfileComboBox.ItemsSource = _profiles;
        ProfileComboBox.SelectedIndex = _profiles.Count > 0 ? 0 : -1;
        HintText.Text = pdfSupported
            ? "Markdown and PDF formats are available."
            : "Markdown is available. PDF export will be enabled when a PDF writer is added.";
    }

    public MemoPublishFormat SelectedFormat => MemoPublishFormat.Markdown;

    public string? SelectedProfileId => ProfileComboBox.SelectedValue?.ToString();

    public bool IncludeCitationsList => IncludeCitationsCheckBox.IsChecked ?? true;

    public bool IncludeEvidenceExcerpts => IncludeExcerptsCheckBox.IsChecked ?? false;

    private void Publish_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
