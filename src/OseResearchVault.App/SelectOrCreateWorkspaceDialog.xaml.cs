using System.Windows;
using OseResearchVault.Core.Interfaces;
using Forms = System.Windows.Forms;

namespace OseResearchVault.App;

public partial class SelectOrCreateWorkspaceDialog : Window
{
    private readonly IWorkspaceService _workspaceService;

    public SelectOrCreateWorkspaceDialog(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        WorkspaceGrid.ItemsSource = await _workspaceService.ListAsync();
    }

    private async void UseSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkspaceGrid.SelectedItem is not Core.Models.WorkspaceSummary selected)
        {
            MessageBox.Show(this, "Select a workspace.");
            return;
        }

        if (await _workspaceService.SwitchAsync(selected.Id))
        {
            DialogResult = true;
        }
    }

    private async void CreateWorkspace_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _workspaceService.CreateAsync(WorkspaceNameTextBox.Text, WorkspaceFolderTextBox.Text);
            DialogResult = true;
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

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
