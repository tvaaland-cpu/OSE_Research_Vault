using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly ISnapshotService _snapshotService;
    private readonly IAppSettingsService _appSettingsService;

    public MainWindow(MainViewModel viewModel, IExportService exportService, INotificationService notificationService, IInvestmentMemoService investmentMemoService, IReviewService reviewService, IBackupService backupService, IShareLogService shareLogService, IDiagnosticsService diagnosticsService, ISnapshotService snapshotService, IAppSettingsService appSettingsService)
    {
        _viewModel = viewModel;
        _exportService = exportService;
        _notificationService = notificationService;
        _investmentMemoService = investmentMemoService;
        _reviewService = reviewService;
        _backupService = backupService;
        _shareLogService = shareLogService;
        _diagnosticsService = diagnosticsService;
        _snapshotService = snapshotService;
        _appSettingsService = appSettingsService;
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
        await OpenAiImportDialogAsync(string.Empty, _viewModel.SelectedNoteCompany);
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

    private async void ExportDiagnostics_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Zip archive (*.zip)|*.zip",
            FileName = $"diagnostics-{DateTime.UtcNow:yyyyMMddHHmmss}.zip",
            DefaultExt = ".zip",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        try
        {
            await _diagnosticsService.ExportAsync(dialog.FileName);
            MessageBox.Show(this, $"Diagnostics exported to:\n{dialog.FileName}", "Diagnostics exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to export diagnostics:\n{ex.Message}", "Diagnostics export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.K)
        {
            OpenCommandPalette();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            HandleGlobalSearchShortcut();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.N)
        {
            _ = OpenNewNoteDialogAsync();
            e.Handled = true;
        }
    }

    private void OpenCommandPalette()
    {
        var items = BuildCommandPaletteItems();
        var dialog = new CommandPaletteDialog(items)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            dialog.SelectedItem?.Execute();
        }
    }

    private List<CommandPaletteItem> BuildCommandPaletteItems()
    {
        var items = new List<CommandPaletteItem>
        {
            CreateNavigateItem("Companies"),
            CreateNavigateItem("Documents"),
            CreateNavigateItem("Notes"),
            CreateNavigateItem("Agents"),
            CreateNavigateItem("Search"),
            CreateNavigateItem("Automations"),
            CreateNavigateItem("Inbox"),
            new("New Note", "Quick create item", "new note create", () => _ = OpenNewNoteDialogAsync()),
            new("Import Document", "Quick create item", "import document file", OpenImportDocumentFromPalette),
            new("Quick Capture", "Quick create item", "capture quick inbox url text ai", () => _ = OpenQuickCaptureDialogAsync()),
            new("Run AskMyVault", "Quick create item", "ask my vault run answer", RunAskMyVaultFromPalette),
            new("Export Research Pack", "Quick create item", "export research pack", () => _ = ExportResearchPackFromPaletteAsync())
        };

        foreach (var company in _viewModel.CompanyOptions.Where(x => !string.IsNullOrWhiteSpace(x.Id)))
        {
            var companyCopy = company;
            items.Add(new CommandPaletteItem(
                $"Open Company: {company.DisplayName}",
                "Navigate to Company Hub",
                $"company {company.DisplayName}",
                () => OpenCompanyFromPalette(companyCopy)));
        }

        return items;
    }

    private async Task OpenQuickCaptureDialogAsync()
    {
        var scopedCompany = _viewModel.SelectedHubCompany ?? _viewModel.SelectedNoteCompany;
        var dialog = new QuickCaptureDialog(_viewModel.CompanyOptions, scopedCompany)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.CaptureMode is null)
        {
            return;
        }

        switch (dialog.CaptureMode)
        {
            case QuickCaptureMode.Url:
                await CaptureUrlAsync(dialog.Url, dialog.SelectedCompany?.Id);
                break;
            case QuickCaptureMode.Text:
                await CaptureTextAsync(dialog.TextContent, dialog.SelectedCompany?.Id);
                break;
            case QuickCaptureMode.AiImport:
                await OpenAiImportDialogAsync(dialog.TextContent, dialog.SelectedCompany);
                break;
        }
    }

    private async Task CaptureUrlAsync(string url, string? companyId)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            MessageBox.Show(this, "Enter a valid URL to capture.", "Quick Capture", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var workspaceId = await GetCurrentWorkspaceIdAsync();
            await _snapshotService.SaveUrlSnapshotAsync(url, workspaceId, companyId, "html");
            NavigateTo("Documents");
            await _viewModel.LoadAsync();
            MessageBox.Show(this, "URL captured to Documents inbox.", "Quick Capture", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to capture URL:\n{ex.Message}", "Quick Capture", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CaptureTextAsync(string text, string? companyId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show(this, "Paste text to capture.", "Quick Capture", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var preview = text.Trim();
        var firstLine = preview.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Quick capture";
        var title = firstLine.Length > 80 ? firstLine[..80] : firstLine;

        _viewModel.SelectedNoteCompany = _viewModel.CompanyOptions.FirstOrDefault(c => string.Equals(c.Id, companyId, StringComparison.OrdinalIgnoreCase));
        _viewModel.NoteTitle = title;
        _viewModel.NoteContent = text;
        _viewModel.SelectedNoteType = "log";
        _viewModel.NoteTags = "inbox";

        NavigateTo("Notes");
        if (_viewModel.SaveNoteCommand.CanExecute(null))
        {
            _viewModel.SaveNoteCommand.Execute(null);
        }

        await Task.Delay(10);
        MessageBox.Show(this, "Text captured as inbox note.", "Quick Capture", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task OpenAiImportDialogAsync(string seededResponse, CompanyOptionViewModel? selectedCompany)
    {
        var dialog = new AiImportDialog(seededResponse)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.Request is null)
        {
            return;
        }

        await _viewModel.ImportAiOutputAsync(new AiImportRequest
        {
            Model = dialog.Request.Model,
            Prompt = dialog.Request.Prompt,
            Response = dialog.Request.Response,
            Sources = dialog.Request.Sources,
            CompanyId = selectedCompany?.Id
        });

        NavigateTo("Notes");
    }

    private async Task<string> GetCurrentWorkspaceIdAsync()
    {
        var settings = await _appSettingsService.GetSettingsAsync();
        if (!string.IsNullOrWhiteSpace(settings.CurrentWorkspaceId))
        {
            return settings.CurrentWorkspaceId;
        }

        return settings.Workspaces.FirstOrDefault()?.Id
               ?? throw new InvalidOperationException("No active workspace found.");
    }

    private CommandPaletteItem CreateNavigateItem(string screen)
    {
        return new CommandPaletteItem(
            $"Go to {screen}",
            "Navigate",
            $"navigate {screen}",
            () => NavigateTo(screen));
    }

    private void NavigateTo(string title)
    {
        var item = _viewModel.NavigationItems.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            _viewModel.SelectedItem = item;
        }
    }

    private void OpenCompanyFromPalette(CompanyOptionViewModel company)
    {
        NavigateTo("Company Hub");
        _viewModel.SelectedHubCompany = _viewModel.CompanyOptions.FirstOrDefault(x => string.Equals(x.Id, company.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void HandleGlobalSearchShortcut()
    {
        if (_viewModel.IsSearchSelected)
        {
            GlobalSearchQueryTextBox.Focus();
            GlobalSearchQueryTextBox.SelectAll();
            return;
        }

        NavigateTo("Search");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GlobalSearchQueryTextBox.Focus();
            GlobalSearchQueryTextBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private Task OpenNewNoteDialogAsync()
    {
        var scopedCompany = _viewModel.SelectedHubCompany ?? _viewModel.SelectedNoteCompany;
        var dialog = new NewNoteDialog(_viewModel.CompanyOptions, _viewModel.NoteTypes, scopedCompany)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return Task.CompletedTask;
        }

        NavigateTo("Notes");
        _viewModel.NoteTitle = dialog.NoteTitle;
        _viewModel.NoteContent = dialog.NoteContent;
        _viewModel.SelectedNoteType = dialog.NoteType;
        _viewModel.SelectedNoteCompany = dialog.SelectedCompany;

        if (_viewModel.SaveNoteCommand.CanExecute(null))
        {
            _viewModel.SaveNoteCommand.Execute(null);
        }

        return Task.CompletedTask;
    }

    private void OpenImportDocumentFromPalette()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Supported files (*.pdf;*.docx;*.txt;*.md)|*.pdf;*.docx;*.txt;*.md|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        _ = _viewModel.ImportDocumentsAsync(dialog.FileNames);
    }

    private void RunAskMyVaultFromPalette()
    {
        NavigateTo("Agents");
        if (_viewModel.RunAgentCommand.CanExecute(null))
        {
            _viewModel.RunAgentCommand.Execute(null);
        }
    }

    private async Task ExportResearchPackFromPaletteAsync()
    {
        if (_viewModel.SelectedHubCompany is null)
        {
            NavigateTo("Company Hub");
            MessageBox.Show(this, "Select a company in Company Hub before exporting a research pack.", "Export Research Pack", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await Dispatcher.InvokeAsync(() => ExportResearchPack_OnClick(this, new RoutedEventArgs()));
    }


}
