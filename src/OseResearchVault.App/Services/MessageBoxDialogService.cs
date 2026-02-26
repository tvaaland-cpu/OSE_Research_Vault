using System.Windows;

namespace OseResearchVault.App.Services;

public sealed class MessageBoxDialogService : IUserDialogService
{
    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
