using System.Windows;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using Forms = System.Windows.Forms;

namespace OseResearchVault.App;

public partial class WorkspaceManagerDialog : Window
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IRestoreService _restoreService;

    public WorkspaceManagerDialog(IWorkspaceService workspaceService, IRestoreService restoreService)
    {
        _workspaceService = workspaceService;
        _restoreService = restoreService;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        WorkspaceGrid.ItemsSource = await _workspaceService.ListAsync();
    }

    private async void Switch_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkspaceGrid.SelectedItem is not WorkspaceSummary selected)
        {
            return;
        }

        await _workspaceService.SwitchAsync(selected.Id);
        DialogResult = true;
    }

    private async void DeleteReference_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkspaceGrid.SelectedItem is not WorkspaceSummary selected)
        {
            return;
        }

        if (MessageBox.Show(this, "Delete workspace reference only? Files are not deleted.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await _workspaceService.DeleteReferenceAsync(selected.Id);
        await ReloadAsync();
    }

    private async void Create_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _workspaceService.CreateAsync(WorkspaceNameTextBox.Text, WorkspaceFolderTextBox.Text);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BrowseFolder_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            WorkspaceFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }


    private async void Clone_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkspaceGrid.SelectedItem is not WorkspaceSummary selected)
        {
            return;
        }

        var suggestedFolder = Path.Combine(Path.GetDirectoryName(selected.Path) ?? selected.Path, $"{Path.GetFileName(selected.Path)}-copy");
        var dialog = new CloneWorkspaceDialog(selected.Name, suggestedFolder) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _workspaceService.CloneWorkspaceAsync(selected.Id, dialog.DestinationFolder, dialog.WorkspaceName);
            await ReloadAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Clone Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void RestoreFromBackup_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new RestoreWorkspaceDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (Directory.Exists(dialog.DestinationFolderPath)
                && Directory.EnumerateFileSystemEntries(dialog.DestinationFolderPath).Any()
                && MessageBox.Show(this,
                    "Destination folder is not empty. Existing files may be replaced. Continue?",
                    "Restore Workspace",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (Directory.Exists(dialog.DestinationFolderPath)
                && Directory.EnumerateFileSystemEntries(dialog.DestinationFolderPath).Any())
            {
                Directory.Delete(dialog.DestinationFolderPath, recursive: true);
            }

            var restored = await _restoreService.RestoreWorkspaceFromZipAsync(
                dialog.ZipPath,
                dialog.DestinationFolderPath,
                dialog.DisplayName);

            if (dialog.SwitchToRestoredWorkspace)
            {
                await _workspaceService.SwitchAsync(restored.Id);
                DialogResult = true;
                return;
            }

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Restore Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
