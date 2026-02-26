using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using OseResearchVault.App.ViewModels;
using OseResearchVault.Core.Interfaces;

namespace OseResearchVault.App;

public partial class MainWindow
{
    private async void ManageWorkspaces_OnClick(object sender, RoutedEventArgs e)
    {
        if (App.Services?.GetService(typeof(WorkspaceManagerDialog)) is not WorkspaceManagerDialog dialog)
        {
            return;
        }

        dialog.Owner = this;
        var switched = dialog.ShowDialog() == true;
        if (!switched)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.RefreshForWorkspaceSwitchAsync();
        }
    }
}
