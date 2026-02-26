using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using OseResearchVault.Core.Interfaces;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
using OseResearchVault.App.ViewModels;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IExportService _exportService;
    private readonly INotificationService _notificationService;
    private readonly IInvestmentMemoService _investmentMemoService;
    private readonly IReviewService _reviewService;
    private readonly IBackupService _backupService;
    private readonly IShareLogService _shareLogService;

    public MainWindow(MainViewModel viewModel, IExportService exportService, INotificationService notificationService, IInvestmentMemoService investmentMemoService, IReviewService reviewService, IBackupService backupService, IShareLogService shareLogService)
    {
        _viewModel = viewModel;
        _exportService = exportService;
        _notificationService = notificationService;
        _investmentMemoService = investmentMemoService;
        _reviewService = reviewService;
        _backupService = backupService;
        _shareLogService = shareLogService;
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.AutomationRequested += ViewModelOnAutomationRequested;
    }


    private async void ViewModelOnAutomationRequested(object? sender, MainViewModel.AutomationEditorRequestedEventArgs e)
    {
        var agents = await _viewModel.GetAgentTemplatesAsync();
        var companies = await _viewModel.GetCompaniesAsync();

        var dialog = new AutomationEditorDialog(e.ExistingAutomation, agents, companies)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.Request is null)
        {
            return;
        }

        await _viewModel.SaveAutomationFromDialogAsync(e.ExistingAutomation, dialog.Request);
    }

    private async void DocumentDropArea_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
        {
            return;
        }

        await _viewModel.ImportDocumentsAsync(files);
    }

    private void DocumentDropArea_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void ImportAiOutput_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AiImportDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.Request is null)
        {
            return;
        }

        var request = new AiImportRequest
        {
            Model = dialog.Request.Model,
            Prompt = dialog.Request.Prompt,
            Response = dialog.Request.Response,
            Sources = dialog.Request.Sources,
            CompanyId = _viewModel.SelectedNoteCompany?.Id
        };

        await _viewModel.ImportAiOutputAsync(request);
    }

    private void DocumentPreviewTextBox_OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        CreateSnippetButton.IsEnabled = _viewModel.SelectedDocument is not null && DocumentPreviewTextBox.SelectionLength > 0;
    }

    private async void CreateSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDocument is null || DocumentPreviewTextBox.SelectionLength <= 0)
        {
            return;
        }

        var selectionText = DocumentPreviewTextBox.SelectedText;
        var start = DocumentPreviewTextBox.SelectionStart;
        var end = start + DocumentPreviewTextBox.SelectionLength;
        var defaultLocator = $"sel=offset:{start}-{end}";

        var companyOptions = _viewModel.CompanyOptions.ToList();
        if (!companyOptions.Any(c => string.IsNullOrWhiteSpace(c.Id)))
        {
            companyOptions.Insert(0, new CompanyOptionViewModel { Id = string.Empty, DisplayName = "(No company)" });
        }

        var dialog = new CreateSnippetDialog(
            _viewModel.SelectedDocument.Title,
            companyOptions,
            _viewModel.SelectedDocument.CompanyId,
            defaultLocator,
            selectionText)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _viewModel.CreateSnippetForSelectedDocumentAsync(dialog.Locator, dialog.SnippetTextValue, dialog.CompanyId);
    }


    private async void CreateMetricFromSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DocumentSnippetListItemViewModel snippet)
        {
            return;
        }

        var companies = _viewModel.CompanyOptions.ToList();
        var currency = _viewModel.Companies.FirstOrDefault(c => c.Id == snippet.CompanyId)?.Currency;
        var dialog = new CreateMetricDialog(
            companies,
            snippet.CompanyId,
            snippet.DocumentTitle,
            snippet.Locator,
            snippet.Text,
            currency)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _viewModel.CreateMetricFromSnippetAsync(
            snippet.Id,
            dialog.CompanyId ?? string.Empty,
            dialog.MetricName,
            dialog.Period,
            dialog.Value,
            dialog.Unit,
            dialog.Currency);
    }

    private void CopyCitation_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string citation || string.IsNullOrWhiteSpace(citation))
        {
            return;
        }

        Clipboard.SetText(citation);
        _viewModel.AgentStatusMessage = "Citation copied to clipboard.";
    }

    private void CopyPrompt_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.RunPromptText))
        {
            return;
        }

        Clipboard.SetText(_viewModel.RunPromptText);
        _viewModel.AgentStatusMessage = "Prompt copied to clipboard.";
    }

    private async void CreateSnippetAndLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRunArtifact is null)
        {
            return;
        }

        var suggestedDocuments = _viewModel.GetSuggestedDocumentsForSelectedArtifact();
        if (suggestedDocuments.Count == 0)
        {
            MessageBox.Show(this, "No documents are available to create evidence.", "Create Snippet & Link", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var documentChoices = suggestedDocuments
            .Select(d => new CreateSnippetAndLinkDialog.DocumentChoice
            {
                Id = d.Id,
                DisplayName = string.IsNullOrWhiteSpace(d.Company) ? d.Title : $"{d.Title} ({d.Company})",
                CompanyId = string.IsNullOrWhiteSpace(d.CompanyId) ? null : d.CompanyId
            })
            .ToList();

        var dialog = new CreateSnippetAndLinkDialog(documentChoices, _viewModel.GetDocumentDetailsForEvidenceAsync)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _viewModel.CreateSnippetAndLinkToArtifactAsync(
            _viewModel.SelectedRunArtifact.Id,
            dialog.SelectedDocumentId,
            dialog.Locator,
            dialog.Snippet,
            dialog.SelectedCompanyId);
    }

    private async void LinkArtifactSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRunArtifact is null)
        {
            MessageBox.Show(this, "Select an artifact first.", "Link Snippet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var defaultCompanyId = string.IsNullOrWhiteSpace(_viewModel.SelectedAgentRun?.CompanyId)
            ? null
            : _viewModel.SelectedAgentRun.CompanyId;

        var companyOptions = new List<CompanyOptionViewModel> { new() { Id = string.Empty, DisplayName = "All companies" } };
        companyOptions.AddRange(_viewModel.CompanyOptions);

        var dialog = new LinkSnippetDialog(companyOptions, _viewModel.Documents.ToList(), defaultCompanyId, _viewModel.SearchSnippetsForLinkingAsync)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.SelectedSnippet is null)
        {
            return;
        }

        await _viewModel.LinkSnippetToSelectedArtifactAsync(dialog.SelectedSnippet.Id);
    }

    private async void RemoveEvidenceLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string evidenceLinkId } || string.IsNullOrWhiteSpace(evidenceLinkId))
        {
            return;
        }

        await _viewModel.RemoveEvidenceLinkAsync(evidenceLinkId);
    }

    private void EvidenceDocumentLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Hyperlink { Tag: string documentId } || string.IsNullOrWhiteSpace(documentId))
        {
            return;
        }

        _viewModel.OpenDocumentDetails(documentId);
    }


    private async void GenerateWeeklyReview_OnClick(object sender, RoutedEventArgs e)
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _reviewService.GenerateWeeklyReviewAsync(string.Empty, asOfDate);
        await _notificationService.AddNotification("info", "Weekly review generated", $"{result.NoteTitle} created with {result.ImportedDocumentCount} new documents and {result.RecentTradeCount} recent trades.");
        await _viewModel.RefreshAfterWeeklyReviewAsync();

        MessageBox.Show(this, $"Created note: {result.NoteTitle}", "Weekly review", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void GenerateInvestmentMemo_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHubCompany is null)
        {
            MessageBox.Show(this, "Select a company first.", "Investment Memo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = await _investmentMemoService.GenerateInvestmentMemoAsync(_viewModel.SelectedHubCompany.Id, _viewModel.SelectedHubCompany.DisplayName);
        await _viewModel.RefreshAfterInvestmentMemoAsync(_viewModel.SelectedHubCompany.Id);

        var message = result.CitationsDetected
            ? "Investment memo note created with citations."
            : "Investment memo note created. No citations detected.";

        _viewModel.InvestmentMemoStatusMessage = result.CitationsDetected ? "Citations detected" : "No citations detected";
        MessageBox.Show(this, message, "Investment Memo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void GenerateQuarterlyReview_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHubCompany is null)
        {
            MessageBox.Show(this, "Select a company first.", "Quarterly Review", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var now = DateTime.UtcNow;
        var quarter = ((now.Month - 1) / 3) + 1;
        var dialog = new QuarterlyReviewPeriodDialog($"{now.Year}Q{quarter}")
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var result = await _reviewService.GenerateQuarterlyCompanyReviewAsync(string.Empty, _viewModel.SelectedHubCompany.Id, dialog.PeriodLabel);
        await _notificationService.AddNotification("info", "Quarterly review generated", $"{result.NoteTitle} created with {result.DocumentCount} documents and {result.JournalEntriesCount} journal entries.");
        await _viewModel.RefreshAfterQuarterlyReviewAsync(_viewModel.SelectedHubCompany.Id);

        MessageBox.Show(this, $"Created note: {result.NoteTitle}", "Quarterly Review", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ExportResearchPack_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHubCompany is null)
        {
            MessageBox.Show(this, "Select a company first.", "Export Research Pack", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose an export folder for the company research pack."
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        var profiles = await _exportService.GetExportProfilesAsync(string.Empty);
        var exportOptionsDialog = new ExportResearchPackDialog(profiles)
        {
            Owner = this
        };

        if (exportOptionsDialog.ShowDialog() != true)
        {
            return;
        }

        var selectedProfile = profiles.FirstOrDefault(p => p.ProfileId == exportOptionsDialog.SelectedProfileId);
        var redactionOptions = selectedProfile?.Options ?? new RedactionOptions();
        if (!exportOptionsDialog.ApplyRedaction)
        {
            redactionOptions = new RedactionOptions
            {
                MaskEmails = false,
                MaskPhones = false,
                MaskPaths = false,
                MaskSecrets = false,
                ExcludePrivateTaggedItems = exportOptionsDialog.ExcludePrivateTaggedItems
            };
        }
        else
        {
            redactionOptions.ExcludePrivateTaggedItems = exportOptionsDialog.ExcludePrivateTaggedItems;
        }

        var targetFolder = Path.Combine(dialog.SelectedPath, $"research-pack-{_viewModel.SelectedHubCompany.DisplayName}-{DateTime.UtcNow:yyyyMMddHHmmss}");
        await _exportService.ExportCompanyResearchPackAsync(string.Empty, _viewModel.SelectedHubCompany.Id, targetFolder, redactionOptions);
        await _shareLogService.AddAsync(new ShareLogCreateRequest
        {
            WorkspaceId = string.Empty,
            Action = "export_pack",
            TargetCompanyId = _viewModel.SelectedHubCompany.Id,
            ProfileId = selectedProfile?.ProfileId,
            OutputPath = targetFolder,
            Summary = $"Research pack for {_viewModel.SelectedHubCompany.DisplayName}"
        });
        await RefreshShareLogAsync();
        await _notificationService.AddNotification("info", "Research pack exported", $"Research pack saved to: {targetFolder}");
        MessageBox.Show(this, $"Research pack exported to:
{targetFolder}", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private async void ExportWorkspaceBackup_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Zip archive (*.zip)|*.zip",
            FileName = $"workspace-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.zip",
            DefaultExt = ".zip",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        try
        {
            WorkspaceBackupProgress.Visibility = Visibility.Visible;
            WorkspaceBackupStatusText.Text = "Exporting workspace backup...";

            await _backupService.ExportWorkspaceBackupAsync(string.Empty, dialog.FileName);
            await _shareLogService.AddAsync(new ShareLogCreateRequest
            {
                WorkspaceId = string.Empty,
                Action = "backup_export",
                OutputPath = dialog.FileName,
                Summary = "Workspace backup export"
            });
            await RefreshShareLogAsync();
            await _notificationService.AddNotification("info", "Workspace backup exported", $"Backup saved to: {dialog.FileName}");

            WorkspaceBackupStatusText.Text = $"Backup exported to {dialog.FileName}";
            MessageBox.Show(this, $"Workspace backup exported to:
{dialog.FileName}", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WorkspaceBackupStatusText.Text = "Backup export failed.";
            MessageBox.Show(this, $"Failed to export workspace backup:
{ex.Message}", "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            WorkspaceBackupProgress.Visibility = Visibility.Collapsed;
        }
    }

    private async void FetchAnnouncements_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHubCompany is null)
        {
            MessageBox.Show(this, "Select a company first.", "Fetch announcements", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new FetchAnnouncementsDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var summary = await _viewModel.FetchAnnouncementsForSelectedCompanyAsync(dialog.Days, dialog.ManualUrls);
        MessageBox.Show(this, summary, "Fetch announcements", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ImportPricesCsv_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHubCompany is null)
        {
            MessageBox.Show(this, "Select a company first.", "Import prices", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        try
        {
            var message = await _viewModel.ImportPricesCsvAsync(dialog.FileName);
            MessageBox.Show(this, message, "Import prices", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to import prices:
{ex.Message}", "Import prices", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


}
