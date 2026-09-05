using Domain.Entities;
using RikiPath.Application.Common;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notifications;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần các hàm repo sau:
    //   INotificationRepository.GetByUserAsync(userId, onlyUnread, page, pageSize) -> (items, total)
    //   INotificationRepository.CountUnreadAsync(userId) -> int
    //   INotificationRepository.MarkAllAsReadAsync(userId) -> atomic UPDATE ... SET IsRead=true
    //     WHERE UserId=@userId AND IsRead=false (ExecuteUpdateAsync, giống pattern race-condition
    //     ở ConsultationService, tránh phải load hết N bản ghi lên rồi save từng cái).
    public class NotificationService(IUnitOfWork unitOfWork) : INotificationService
    {
        public async Task<ApiResponse<PagedResult<NotificationResponse>>> GetMyNotificationsAsync(
            int userId, bool onlyUnread, int page, int pageSize, CancellationToken cancellationToken)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var (items, total) = await unitOfWork.Notifications.GetByUserAsync(userId, onlyUnread, page, pageSize);

                var result = new PagedResult<NotificationResponse>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = total,
                    Items = items.Select(MapToResponse).ToList(),
                };

                return ApiResponse<PagedResult<NotificationResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResult<NotificationResponse>>.Fail(
                    "Không thể tải danh sách thông báo.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<int>> GetUnreadCountAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                var count = await unitOfWork.Notifications.CountUnreadAsync(userId);
                return ApiResponse<int>.Success(count);
            }
            catch (Exception ex)
            {
                return ApiResponse<int>.Fail(
                    "Không thể đếm thông báo chưa đọc.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken)
        {
            try
            {
                var notification = await unitOfWork.Notifications.GetByIdAsync(notificationId);
                if (notification is null)
                    return ApiResponse.NotFound($"Không tìm thấy thông báo Id = {notificationId}.");

                if (notification.UserId != userId)
                    return ApiResponse.Fail("Thông báo này không thuộc về bạn.", HttpStatusCode.Forbidden);

                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    unitOfWork.Notifications.Update(notification);
                    await unitOfWork.SaveChangesAsync();
                }

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể đánh dấu đã đọc.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                await unitOfWork.Notifications.MarkAllAsReadAsync(userId);
                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể đánh dấu tất cả đã đọc.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task CreateNotificationAsync(
            int userId, string title, string message, string type, CancellationToken cancellationToken)
        {
            // Fire-and-forget từ các service khác — không throw ra ngoài để không làm hỏng luồng
            // nghiệp vụ chính (VD: chấm điểm AI thành công nhưng tạo notification lỗi thì vẫn
            // phải trả kết quả chấm điểm cho user).
            try
            {
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
                if (user is null || !user.SystemNotificationsEnabled)
                    return;

                await unitOfWork.Notifications.AddAsync(new Notification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = type,
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow,
                });
                await unitOfWork.SaveChangesAsync();
            }
            catch
            {
                // best-effort, nuốt lỗi có chủ đích — xem comment phía trên
            }
        }

        private static NotificationResponse MapToResponse(Notification n) => new()
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedDate.Value,
        };

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
