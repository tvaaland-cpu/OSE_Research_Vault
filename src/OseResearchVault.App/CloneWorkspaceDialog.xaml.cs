using System.Windows;
using Forms = System.Windows.Forms;

namespace OseResearchVault.App;

public partial class CloneWorkspaceDialog : Window
{
    public CloneWorkspaceDialog(string sourceWorkspaceName, string suggestedDestinationFolder)
    {
        InitializeComponent();
        WorkspaceNameTextBox.Text = $"{sourceWorkspaceName} (Copy)";
        DestinationFolderTextBox.Text = suggestedDestinationFolder;
    }

    public string WorkspaceName => WorkspaceNameTextBox.Text.Trim();
    public string DestinationFolder => DestinationFolderTextBox.Text.Trim();

    private void BrowseFolder_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            DestinationFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Clone_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WorkspaceName) || string.IsNullOrWhiteSpace(DestinationFolder))
        {
            MessageBox.Show(this, "Enter both a name and destination folder.", "Clone Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
