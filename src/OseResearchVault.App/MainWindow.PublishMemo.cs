using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class MainWindow
{
    private async void PublishMemo_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNote is null)
        {
            MessageBox.Show(this, "Select a memo note first.", "Publish Memo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var publishService = App.Services?.GetService<IMemoPublishService>();
        if (publishService is null)
        {
            MessageBox.Show(this, "Publish service is unavailable.", "Publish Memo", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var profiles = await _exportService.GetExportProfilesAsync(string.Empty);
        var dialog = new PublishMemoDialog(profiles, publishService.SupportsPdf)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var selectedProfile = profiles.FirstOrDefault(p => p.ProfileId == dialog.SelectedProfileId);
        var redactionOptions = selectedProfile?.Options ?? new RedactionOptions();

        var result = await publishService.PublishAsync(new MemoPublishRequest
        {
            NoteId = _viewModel.SelectedNote.Id,
            NoteTitle = _viewModel.SelectedNote.Title,
            NoteContent = _viewModel.SelectedNote.Content,
            CompanyId = string.IsNullOrWhiteSpace(_viewModel.SelectedNote.CompanyId) ? null : _viewModel.SelectedNote.CompanyId,
            CompanyName = _viewModel.SelectedNote.CompanyName,
            Format = dialog.SelectedFormat,
            RedactionOptions = redactionOptions,
            IncludeCitationsList = dialog.IncludeCitationsList,
            IncludeEvidenceExcerpts = dialog.IncludeEvidenceExcerpts
        });

        await _notificationService.AddNotification("info", "Memo published", $"Memo exported to {result.OutputFilePath}");
        MessageBox.Show(this, $"Published memo to:\n{result.OutputFilePath}", "Publish Memo", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
