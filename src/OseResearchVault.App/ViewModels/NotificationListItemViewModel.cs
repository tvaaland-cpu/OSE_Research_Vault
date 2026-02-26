namespace OseResearchVault.App.ViewModels;

public sealed class NotificationListItemViewModel : ViewModelBase
{
    private bool _isRead;

    public string NotificationId { get; init; } = string.Empty;
    public string Level { get; init; } = "info";
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;

    public bool IsRead
    {
        get => _isRead;
        set
        {
            if (SetProperty(ref _isRead, value))
            {
                OnPropertyChanged(nameof(ReadStatus));
            }
        }
    }

    public string ReadStatus => IsRead ? "Read" : "Unread";
}
