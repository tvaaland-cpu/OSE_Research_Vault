namespace OseResearchVault.App.ViewModels;

public sealed class TagSelectionViewModel : ViewModelBase
{
    private bool _isSelected;

    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
