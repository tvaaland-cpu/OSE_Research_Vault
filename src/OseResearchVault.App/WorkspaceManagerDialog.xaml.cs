using System.Windows;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using Forms = System.Windows.Forms;

namespace OseResearchVault.App;

public partial class WorkspaceManagerDialog : Window
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceManagerDialog(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
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
}
