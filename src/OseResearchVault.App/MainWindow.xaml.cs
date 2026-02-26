using System.Linq;
using System.Windows;
using OseResearchVault.App.ViewModels;
using OseResearchVault.Core.Models;

namespace OseResearchVault.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
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
}
