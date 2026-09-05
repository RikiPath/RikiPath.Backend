using RikiPath.Application.Common;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notifications;

namespace RikiPath.Application.IServices
{
    public interface INotificationService
    {
        Task<ApiResponse<PagedResult<NotificationResponse>>> GetMyNotificationsAsync(
            int userId, bool onlyUnread, int page, int pageSize, CancellationToken cancellationToken);

        Task<ApiResponse<int>> GetUnreadCountAsync(int userId, CancellationToken cancellationToken);

        Task<ApiResponse> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken);

        Task<ApiResponse> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken);

        /// <summary>
        /// Dùng NỘI BỘ bởi các service khác (ReviewService nhắc ôn SM-2, GradingService báo kết quả
        /// chấm bài, ConsultationService nhắc lịch hẹn...) để tạo thông báo — không expose qua Controller.
        /// EmailNotificationsEnabled/SystemNotificationsEnabled của user quyết định có thật sự gửi hay không.
        /// </summary>
        Task CreateNotificationAsync(int userId, string title, string message, string type, CancellationToken cancellationToken);
    }
}
