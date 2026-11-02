using RikiPath.Application.Common;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notifications;

namespace RikiPath.Application.IServices
{
    public interface INotificationService
    {
        Task<ApiResponse<PagedResult<NotificationResponse>>> GetMyNotificationsAsync(bool onlyUnread, int page, int pageSize, CancellationToken cancellationToken);

        Task<ApiResponse<int>> GetUnreadCountAsync(CancellationToken cancellationToken);

        Task<ApiResponse> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken);

        Task<ApiResponse> MarkAllAsReadAsync(CancellationToken cancellationToken);

        Task CreateNotificationAsync(string title, string message, string type, CancellationToken cancellationToken);

        Task<ApiResponse<NotificationResponse>> SendNotificationToUserAsync(int targetUserId, string title, string message, string type, string? linkUrl, CancellationToken cancellationToken, string? idempotencyKey = null);
    }
}
