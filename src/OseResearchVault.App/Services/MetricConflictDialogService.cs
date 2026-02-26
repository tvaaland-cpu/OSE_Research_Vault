using System.Windows;

namespace OseResearchVault.App.Services;

public sealed class MetricConflictDialogService : IMetricConflictDialogService
{
    public MetricConflictDialogChoice ShowMetricConflictDialog()
    {
        var result = MessageBox.Show(
            "Metric already exists. Replace / Create anyway / Cancel",
            "Metric conflict",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => MetricConflictDialogChoice.Replace,
            MessageBoxResult.No => MetricConflictDialogChoice.CreateAnyway,
            _ => MetricConflictDialogChoice.Cancel
        };
    }
}
