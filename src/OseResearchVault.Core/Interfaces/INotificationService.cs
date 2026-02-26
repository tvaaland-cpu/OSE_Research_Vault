using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface INotificationService
{
    Task AddNotification(string level, string title, string body, CancellationToken cancellationToken = default);
    Task MarkRead(string notificationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRecord>> ListNotifications(string workspaceId, bool unreadOnly, CancellationToken cancellationToken = default);
}
