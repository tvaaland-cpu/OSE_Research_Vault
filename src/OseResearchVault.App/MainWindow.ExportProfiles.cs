using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.App;

public partial class MainWindow
{
    private string? _selectedExportProfileId;

    private sealed class ExportProfileListItem
    {
        public string ProfileId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public RedactionOptions Options { get; init; } = new();
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshExportProfilesAsync();
    }

    private async Task RefreshExportProfilesAsync()
    {
        var profiles = await _exportService.GetExportProfilesAsync(string.Empty);
        ExportProfilesListBox.ItemsSource = profiles
            .Select(p => new ExportProfileListItem { ProfileId = p.ProfileId, Name = p.Name, Options = p.Options })
            .ToList();
    }

    private void ExportProfilesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExportProfilesListBox.SelectedItem is not ExportProfileListItem item)
        {
            return;
        }

        _selectedExportProfileId = item.ProfileId;
        ExportProfileNameTextBox.Text = item.Name;
        MaskEmailsCheckBox.IsChecked = item.Options.MaskEmails;
        MaskPhonesCheckBox.IsChecked = item.Options.MaskPhones;
        MaskPathsCheckBox.IsChecked = item.Options.MaskPaths;
        MaskSecretsCheckBox.IsChecked = item.Options.MaskSecrets;
        ExcludePrivateCheckBox.IsChecked = item.Options.ExcludePrivateTaggedItems;
    }

    private async void SaveExportProfile_OnClick(object sender, RoutedEventArgs e)
    {
        var request = new ExportProfileUpsertRequest
        {
            ProfileId = _selectedExportProfileId,
            Name = ExportProfileNameTextBox.Text,
            Options = ReadRedactionOptionsFromForm()
        };

        await _exportService.SaveExportProfileAsync(string.Empty, request);
        await RefreshExportProfilesAsync();
    }

    private async void DeleteExportProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedExportProfileId))
        {
            return;
        }

        await _exportService.DeleteExportProfileAsync(_selectedExportProfileId);
        _selectedExportProfileId = null;
        ExportProfileNameTextBox.Text = string.Empty;
        await RefreshExportProfilesAsync();
    }

    private void PreviewRedaction_OnClick(object sender, RoutedEventArgs e)
    {
        var options = ReadRedactionOptionsFromForm();
        var sourceText = !string.IsNullOrWhiteSpace(_viewModel.SelectedNote?.Content)
            ? _viewModel.SelectedNote.Content
            : _viewModel.DetailsTextPreview;

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            MessageBox.Show(this, "Select a note or document first.", "Preview redaction", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var redaction = new RegexRedactionService().Redact(sourceText, options);
        var dialog = new RedactionPreviewDialog(sourceText, redaction.RedactedText, redaction.Hits.Count)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private RedactionOptions ReadRedactionOptionsFromForm() => new()
    {
        MaskEmails = MaskEmailsCheckBox.IsChecked ?? true,
        MaskPhones = MaskPhonesCheckBox.IsChecked ?? true,
        MaskPaths = MaskPathsCheckBox.IsChecked ?? true,
        MaskSecrets = MaskSecretsCheckBox.IsChecked ?? true,
        ExcludePrivateTaggedItems = ExcludePrivateCheckBox.IsChecked ?? true
    };
}
