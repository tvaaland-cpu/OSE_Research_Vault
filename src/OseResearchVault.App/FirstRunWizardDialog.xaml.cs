using System.Windows;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using Forms = System.Windows.Forms;

namespace OseResearchVault.App;

public partial class FirstRunWizardDialog : Window
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAgentService _agentService;

    public FirstRunWizardDialog(IWorkspaceService workspaceService, IAppSettingsService appSettingsService, IAgentService agentService)
    {
        _workspaceService = workspaceService;
        _appSettingsService = appSettingsService;
        _agentService = agentService;

        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object? sender, RoutedEventArgs e)
    {
        WorkspaceGrid.ItemsSource = await _workspaceService.ListAsync();
        var settings = await _appSettingsService.GetSettingsAsync();
        VaultFolderTextBox.Text = settings.VaultStorageDirectory;
        InboxFolderTextBox.Text = settings.ImportInboxFolderPath;
        EnableInboxCheckBox.IsChecked = settings.ImportInboxEnabled;

        var profiles = await _agentService.GetModelProfilesAsync();
        var options = new List<ModelProfileOption> { new() { ModelProfileId = string.Empty, Name = "Keep current default" } };
        options.AddRange(profiles.Select(p => new ModelProfileOption { ModelProfileId = p.ModelProfileId, Name = p.Name }));
        ModelProfileComboBox.ItemsSource = options;
        ModelProfileComboBox.SelectedIndex = 0;
    }

    private async void Finish_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var workspaceName = WorkspaceNameTextBox.Text.Trim();
            var workspaceFolder = WorkspaceFolderTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(workspaceName) || !string.IsNullOrWhiteSpace(workspaceFolder))
            {
                if (string.IsNullOrWhiteSpace(workspaceName) || string.IsNullOrWhiteSpace(workspaceFolder))
                {
                    MessageBox.Show(this, "Provide both workspace name and workspace folder, or leave both blank to use selected workspace.");
                    return;
                }

                await _workspaceService.CreateAsync(workspaceName, workspaceFolder);
            }
            else if (WorkspaceGrid.SelectedItem is WorkspaceSummary selectedWorkspace)
            {
                await _workspaceService.SwitchAsync(selectedWorkspace.Id);
            }

            var settings = await _appSettingsService.GetSettingsAsync();
            var vaultFolder = VaultFolderTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(vaultFolder))
            {
                settings.VaultStorageDirectory = Path.GetFullPath(vaultFolder);
                Directory.CreateDirectory(settings.VaultStorageDirectory);
            }

            settings.ImportInboxFolderPath = InboxFolderTextBox.Text.Trim();
            settings.ImportInboxEnabled = EnableInboxCheckBox.IsChecked == true;
            settings.FirstRunCompleted = true;
            await _appSettingsService.SaveSettingsAsync(settings);

            if (ModelProfileComboBox.SelectedItem is ModelProfileOption modelProfile && !string.IsNullOrWhiteSpace(modelProfile.ModelProfileId))
            {
                await _agentService.SetDefaultModelProfileAsync(modelProfile.ModelProfileId);
            }

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void BrowseWorkspaceFolder_OnClick(object sender, RoutedEventArgs e) => WorkspaceFolderTextBox.Text = BrowseForFolder(WorkspaceFolderTextBox.Text);
    private void BrowseVaultFolder_OnClick(object sender, RoutedEventArgs e) => VaultFolderTextBox.Text = BrowseForFolder(VaultFolderTextBox.Text);
    private void BrowseInboxFolder_OnClick(object sender, RoutedEventArgs e) => InboxFolderTextBox.Text = BrowseForFolder(InboxFolderTextBox.Text);

    private static string BrowseForFolder(string initialPath)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : initialPath;
    }

    private sealed class ModelProfileOption
    {
        public string ModelProfileId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}
