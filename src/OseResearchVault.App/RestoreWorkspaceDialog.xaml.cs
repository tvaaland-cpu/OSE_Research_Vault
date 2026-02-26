using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace OseResearchVault.App;

public partial class RestoreWorkspaceDialog : Window
{
    public RestoreWorkspaceDialog()
    {
        InitializeComponent();
    }

    public string ZipPath => ZipPathTextBox.Text.Trim();
    public string DestinationFolderPath => DestinationFolderTextBox.Text.Trim();
    public string? DisplayName => string.IsNullOrWhiteSpace(DisplayNameTextBox.Text) ? null : DisplayNameTextBox.Text.Trim();
    public bool SwitchToRestoredWorkspace => SwitchCheckBox.IsChecked == true;

    private void BrowseZip_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Zip archives (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ZipPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseFolder_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            DestinationFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Finish_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ZipPath) || string.IsNullOrWhiteSpace(DestinationFolderPath))
        {
            MessageBox.Show(this, "Select both a backup zip and destination folder.", "Restore Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
