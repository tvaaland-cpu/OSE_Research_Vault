namespace OseResearchVault.App.Services;

public enum MetricConflictDialogChoice
{
    Replace = 0,
    CreateAnyway = 1,
    Cancel = 2
}

public interface IMetricConflictDialogService
{
    MetricConflictDialogChoice ShowMetricConflictDialog();
}
