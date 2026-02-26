namespace OseResearchVault.App.ViewModels;

public sealed class AutomationListItemViewModel
{
    public string Name { get; init; } = string.Empty;
    public string ScheduleSummary { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
public sealed class AutomationListItemViewModel : ViewModelBase
{
    private bool _enabled;

    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Schedule { get; init; } = string.Empty;
    public string NextRun { get; init; } = string.Empty;
    public string LastRun { get; init; } = string.Empty;
    public string LastStatus { get; init; } = string.Empty;

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }
}
