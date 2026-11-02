using Microsoft.EntityFrameworkCore;
using System.Net;

using Microsoft.AspNetCore.SignalR;

using RikiPath.Application.Common;

using RikiPath.Application.Hubs;

using RikiPath.Application.IServices;

using RikiPath.Application.Responses;

using RikiPath.Application.Responses.Notifications;

using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services

{

    // Cung cấp dịch vụ thông báo người dùng và đồng bộ thời gian thực qua SignalR

    public class NotificationService(

        IUnitOfWork unitOfWork,

        IClaimService claimService,

        IHubContext<NotificationHub>? hubContext = null,

        IFcmPushService? fcmPushService = null) : INotificationService

    {

        public async Task<ApiResponse<PagedResult<NotificationResponse>>> GetMyNotificationsAsync(

            bool onlyUnread, int page, int pageSize, CancellationToken cancellationToken)

        {

            try

            {

                var userId = claimService.GetUserClaim().Id;

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

        public async Task<ApiResponse<int>> GetUnreadCountAsync(CancellationToken cancellationToken)

        {

            try

            {

                var userId = claimService.GetUserClaim().Id;

                var count = await unitOfWork.Notifications.CountUnreadAsync(userId);

                return ApiResponse<int>.Success(count);

            }

            catch (Exception ex)

            {

                return ApiResponse<int>.Fail(

                    "Không thể đếm thông báo chưa đọc.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));

            }

        }

        public async Task<ApiResponse> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken)

        {

            try

            {

                var userId = claimService.GetUserClaim().Id;

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

                    if (hubContext is not null)

                    {

                        try

                        {

                            var userGroup = $"user_{userId}";

                            await hubContext.Clients.Group(userGroup).SendAsync("NotificationRead", notificationId, cancellationToken);

                            var unreadCount = await unitOfWork.Notifications.CountUnreadAsync(userId);

                            await hubContext.Clients.Group(userGroup).SendAsync("UnreadCountChanged", unreadCount, cancellationToken);

                        }

                        catch

                        {

                            // SignalR broadcast error is non-blocking

                        }

                    }

                }

                return ApiResponse.Success();

            }

            catch (Exception ex)

            {

                return ApiResponse.Fail(

                    "Không thể đánh dấu đã đọc.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));

            }

        }

        public async Task<ApiResponse> MarkAllAsReadAsync(CancellationToken cancellationToken)

        {

            try

            {

                var userId = claimService.GetUserClaim().Id;

                await unitOfWork.Notifications.MarkAllAsReadAsync(userId);

                if (hubContext is not null)

                {

                    try

                    {

                        var userGroup = $"user_{userId}";

                        await hubContext.Clients.Group(userGroup).SendAsync("UnreadCountChanged", 0, cancellationToken);

                    }

                    catch

                    {

                        // SignalR broadcast error is non-blocking

                    }

                }

                return ApiResponse.Success();

            }

            catch (Exception ex)

            {

                return ApiResponse.Fail(

                    "Không thể đánh dấu tất cả đã đọc.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));

            }

        }

        public async Task CreateNotificationAsync(string title, string message, string type, CancellationToken cancellationToken)

        {

            try

            {

                var userId = claimService.GetUserClaim().Id;

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId);

                if (user is null || !user.SystemNotificationsEnabled)

                    return;

                var notification = new Notification

                {

                    UserId = userId,

                    Title = title,

                    Message = message,

                    Type = type,

                    IsRead = false,

                    CreatedDate = DateTime.UtcNow,

                };

                await unitOfWork.Notifications.AddAsync(notification);

                await unitOfWork.SaveChangesAsync();

                if (hubContext is not null)

                {

                    try

                    {

                        var userGroup = $"user_{userId}";

                        await hubContext.Clients.Group(userGroup).SendAsync("NotificationReceived", MapToResponse(notification), cancellationToken);

                        var unreadCount = await unitOfWork.Notifications.CountUnreadAsync(userId);

                        await hubContext.Clients.Group(userGroup).SendAsync("UnreadCountChanged", unreadCount, cancellationToken);

                    }

                    catch

                    {

                        // best-effort SignalR push

                    }

                }

                if (fcmPushService is not null)

                {

                    try

                    {

                        var fcmPayload = new FcmPushPayload

                        {

                            Title = title,

                            Body = message,

                            Data = new Dictionary<string, string>

                            {

                                ["type"] = type,

                                ["notificationId"] = notification.Id.ToString(),

                                ["click_action"] = "FLUTTER_NOTIFICATION_CLICK"

                            }

                        };

                        await fcmPushService.SendPushToUserAsync(userId, fcmPayload, cancellationToken);

                    }

                    catch

                    {

                        // FCM push failure is non-blocking

                    }

                }

            }

            catch

            {

                // best-effort

            }

        }

                public async Task<ApiResponse<NotificationResponse>> SendNotificationToUserAsync(
            int targetUserId,
            string title,
            string message,
            string type,
            string? linkUrl,
            CancellationToken cancellationToken,
            string? idempotencyKey = null)
        {
            try
            {
                Notification? notification = null;

                // 1. Precise idempotency check via IdempotencyKey (e.g. from outbox dispatcher)
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    notification = await unitOfWork.Notifications.FirstOrDefaultAsync(
                        n => n.IdempotencyKey == idempotencyKey,
                        cancellationToken);
                }
                else if (!string.IsNullOrEmpty(linkUrl))
                {
                    // Fallback heuristic only when caller did not supply an idempotency key
                    var cutoff = DateTime.UtcNow.AddHours(-2);
                    notification = await unitOfWork.Notifications.FirstOrDefaultAsync(
                        n => n.UserId == targetUserId &&
                             n.Type == type &&
                             n.Title == title &&
                             n.LinkUrl == linkUrl &&
                             n.CreatedDate >= cutoff,
                        cancellationToken);
                }

                var isNewNotification = notification is null;
                if (isNewNotification)
                {
                    notification = new Notification
                    {
                        UserId = targetUserId,
                        Title = title,
                        Message = message,
                        Type = type,
                        LinkUrl = linkUrl,
                        IsRead = false,
                        CreatedDate = DateTime.UtcNow,
                        IdempotencyKey = idempotencyKey,
                    };

                    try
                    {
                        await unitOfWork.Notifications.AddAsync(notification);
                        await unitOfWork.SaveChangesAsync();
                    }
                    catch (DbUpdateException) when (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        // Concurrency protection: If a concurrent worker race-inserted the same idempotency key,
                        // handle the unique constraint gracefully and retrieve the winning canonical notification.
                        notification = await unitOfWork.Notifications.FirstOrDefaultAsync(
                            n => n.IdempotencyKey == idempotencyKey,
                            cancellationToken);
                        isNewNotification = false;
                    }
                }

                var response = MapToResponse(notification!);

                // 2. Broadcast SignalR ONLY for new notifications (never rebroadcast duplicate toast or badge increments on outbox retry)
                if (isNewNotification && hubContext is not null)
                {
                    try
                    {
                        var userGroup = $"user_{targetUserId}";
                        await hubContext.Clients.Group(userGroup).SendAsync("NotificationReceived", response, cancellationToken);
                        var unreadCount = await unitOfWork.Notifications.CountUnreadAsync(targetUserId);
                        await hubContext.Clients.Group(userGroup).SendAsync("UnreadCountChanged", unreadCount, cancellationToken);
                    }
                    catch
                    {
                        // best-effort SignalR push
                    }
                }

                // 3. Per-Device FCM push delivery and tracking:
                if (fcmPushService is not null)
                {
                    try
                    {
                        var deliveredTokensSet = new HashSet<string>(StringComparer.Ordinal);
                        if (!string.IsNullOrEmpty(notification!.DeliveredTokens))
                        {
                            var tokens = notification.DeliveredTokens.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            foreach (var tok in tokens) deliveredTokensSet.Add(tok);
                        }

                        var activeTokens = await unitOfWork.UserDeviceTokens.GetActiveTokensByUserIdAsync(targetUserId, cancellationToken);
                        var pendingTokens = activeTokens
                            .Select(t => t.FcmToken.Trim())
                            .Where(t => !string.IsNullOrEmpty(t) && !deliveredTokensSet.Contains(t))
                            .Distinct()
                            .ToList();

                        var fcmPayload = new FcmPushPayload
                        {
                            Title = title,
                            Body = message,
                            Data = new Dictionary<string, string>
                            {
                                ["type"] = type,
                                ["linkUrl"] = linkUrl ?? string.Empty,
                                ["notificationId"] = notification.Id.ToString(),
                                ["click_action"] = "FLUTTER_NOTIFICATION_CLICK"
                            }
                        };

                        if (!string.IsNullOrEmpty(linkUrl))
                        {
                            if (linkUrl.Contains("/bookings/"))
                            {
                                var segs = linkUrl.Split('/');
                                if (segs.Length > 0 && int.TryParse(segs[^1], out var bId))
                                {
                                    fcmPayload.Data["bookingId"] = bId.ToString();
                                    fcmPayload.Data["screen"] = "booking_detail";
                                }
                            }
                            else if (linkUrl.Contains("/meetings/"))
                            {
                                var segs = linkUrl.Split('/');
                                if (segs.Length > 0 && int.TryParse(segs[^1], out var mId))
                                {
                                    fcmPayload.Data["meetingId"] = mId.ToString();
                                    fcmPayload.Data["screen"] = "meeting_room";
                                }
                            }
                        }

                        FcmSendResult? pushResult = null;
                        if (pendingTokens.Count > 0)
                        {
                            pushResult = await fcmPushService.SendPushToTokensAsync(pendingTokens, fcmPayload, cancellationToken);

                            // Record newly delivered tokens to prevent re-delivering on retry
                            var newlyDelivered = pushResult.SuccessfulTokens.Count > 0
                                ? pushResult.SuccessfulTokens
                                : (pushResult.Success ? pendingTokens : new List<string>());

                            var updatedTokens = false;
                            foreach (var tok in newlyDelivered)
                            {
                                if (deliveredTokensSet.Add(tok))
                                {
                                    updatedTokens = true;
                                }
                            }

                            if (updatedTokens)
                            {
                                try
                                {
                                    notification.DeliveredTokens = string.Join(";", deliveredTokensSet);
                                    unitOfWork.Notifications.Update(notification);
                                    await unitOfWork.SaveChangesAsync();
                                }
                                catch (DbUpdateConcurrencyException)
                                {
                                    var fresh = await unitOfWork.Notifications.GetByIdAsync(notification.Id);
                                    if (fresh is not null)
                                    {
                                        if (!string.IsNullOrEmpty(fresh.DeliveredTokens))
                                        {
                                            var tokens = fresh.DeliveredTokens.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                            foreach (var tok in tokens) deliveredTokensSet.Add(tok);
                                        }
                                        fresh.DeliveredTokens = string.Join(";", deliveredTokensSet);
                                        unitOfWork.Notifications.Update(fresh);
                                        await unitOfWork.SaveChangesAsync();
                                    }
                                }
                            }
                        }
                        else if (deliveredTokensSet.Count == 0 && activeTokens.Count == 0)
                        {
                            // If user has no tokens in repository (or in test mocks where SendPushToUserAsync is configured directly):
                            pushResult = await fcmPushService.SendPushToUserAsync(targetUserId, fcmPayload, cancellationToken);
                        }

                        if (pushResult is not null && pushResult.HasDeferredRetry)
                        {
                            response.HasDeferredPushRetry = true;
                            response.PushBackoffSeconds = pushResult.RecommendedBackoffSeconds;
                        }
                    }
                    catch
                    {
                        // FCM push failure is non-blocking
                    }
                }

                return ApiResponse<NotificationResponse>.Success(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<NotificationResponse>.Fail(
                    "Không thể gửi thông báo tới người dùng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private static NotificationResponse MapToResponse(Notification n) => new()

        {

            Id = n.Id,

            Title = n.Title,

            Message = n.Message,

            Type = n.Type,

            LinkUrl = n.LinkUrl,

            IsRead = n.IsRead,

            CreatedAt = n.CreatedDate ?? DateTime.UtcNow,

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
