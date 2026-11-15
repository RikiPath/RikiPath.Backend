using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Services;

public class MentorOutboxDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MentorOutboxDispatcherService> _logger;

    public MentorOutboxDispatcherService(
        IServiceScopeFactory scopeFactory,
        ILogger<MentorOutboxDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MentorOutboxDispatcherService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessPendingOutboxEventsAsync(stoppingToken);
                if (processedCount == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during mentor outbox dispatch loop.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("MentorOutboxDispatcherService stopped.");
    }

    public async Task<int> ProcessPendingOutboxEventsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var nowUtc = DateTime.UtcNow;
        List<MentorOutboxEvent> pendingEvents;

        if (dbContext.Database.IsNpgsql())
        {
            // True atomic PostgreSQL worker claim lease via FOR UPDATE SKIP LOCKED
            pendingEvents = await dbContext.MentorOutboxEvents
                .FromSqlRaw(@"
                    UPDATE ""MentorOutboxEvents""
                    SET ""AvailableAtUtc"" = NOW() + INTERVAL '2 minutes'
                    WHERE ""Id"" IN (
                        SELECT ""Id""
                        FROM ""MentorOutboxEvents""
                        WHERE ""DeliveredAtUtc"" IS NULL AND ""AvailableAtUtc"" <= NOW() AND ""Attempts"" < 5
                        ORDER BY ""Id""
                        LIMIT 20
                        FOR UPDATE SKIP LOCKED
                    )
                    RETURNING *")
                .ToListAsync(cancellationToken);
        }
        else
        {
            // In-memory test harness fallback
            pendingEvents = await dbContext.MentorOutboxEvents
                .Where(e => e.DeliveredAtUtc == null && e.AvailableAtUtc <= nowUtc && e.Attempts < 5)
                .OrderBy(e => e.Id)
                .Take(20)
                .ToListAsync(cancellationToken);

            if (pendingEvents.Count == 0) return 0;

            foreach (var evt in pendingEvents)
            {
                evt.AvailableAtUtc = DateTime.UtcNow.AddMinutes(2);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (pendingEvents.Count == 0) return 0;

        var processedCount = 0;

        foreach (var evt in pendingEvents)
        {
            evt.Attempts++;
            var deferredBackoffSeconds = 0;

            async Task DispatchNotificationAsync(int targetUserId, string title, string message, string type, string? linkUrl, CancellationToken ct)
            {
                var idempotencyKey = $"outbox:{evt.Id}:user:{targetUserId}";
                var res = await notificationService.SendNotificationToUserAsync(
                    targetUserId, title, message, type, linkUrl, ct, idempotencyKey);

                if (!res.IsSuccess)
                    throw new InvalidOperationException("Không thể lưu thông báo cho người nhận; sự kiện sẽ được thử lại.");

                if (res.IsSuccess && res.Result?.HasDeferredPushRetry == true)
                {
                    var backoff = res.Result.PushBackoffSeconds ?? 60;
                    deferredBackoffSeconds = Math.Max(deferredBackoffSeconds, backoff);
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(evt.PayloadJson);
                var root = doc.RootElement;

                switch (evt.EventType)
                {
                    case "MentorVerificationSubmitted":
                    case "MentorVerificationResubmitted":
                    {
                        var mentorId = root.GetProperty("MentorId").GetInt32();
                        var title = evt.EventType == "MentorVerificationSubmitted"
                            ? "Hồ sơ cố vấn chờ xét duyệt" : "Hồ sơ cố vấn đã gửi lại";
                        foreach (var adminId in root.GetProperty("AdminIds").EnumerateArray().Select(id => id.GetInt32()).Distinct())
                        {
                            await DispatchNotificationAsync(adminId, title,
                                "Có hồ sơ cố vấn cần xem xét.", "MentorVerification",
                                $"/admin/mentor-verifications?mentorId={mentorId}", cancellationToken);
                        }
                        break;
                    }

                    case "MentorVerificationReviewed":
                    {
                        var mentorId = root.GetProperty("MentorId").GetInt32();
                        var decision = root.GetProperty("Decision").GetString();
                        var message = decision switch
                        {
                            "Approve" => "Hồ sơ cố vấn của bạn đã được phê duyệt. Vui lòng đăng nhập lại để kích hoạt quyền Cố vấn.",
                            "Reject" => "Hồ sơ cố vấn của bạn chưa được phê duyệt. Vui lòng xem lý do trong hồ sơ.",
                            "RequestChanges" => "Vui lòng bổ sung thông tin trong hồ sơ cố vấn.",
                            _ => throw new InvalidOperationException("Quyết định xét duyệt không hợp lệ.")
                        };
                        await DispatchNotificationAsync(mentorId, "Kết quả xét duyệt hồ sơ", message,
                            "MentorVerificationResult", "/mentor/profile", cancellationToken);
                        foreach (var adminId in root.GetProperty("AdminIds").EnumerateArray().Select(id => id.GetInt32()).Distinct())
                        {
                            await DispatchNotificationAsync(adminId, "Hồ sơ cố vấn đã được xử lý",
                                "Trạng thái hồ sơ cố vấn vừa thay đổi.", "MentorVerification",
                                $"/admin/mentor-verifications?mentorId={mentorId}", cancellationToken);
                        }
                        break;
                    }

                    case "BookingCreated":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var learnerId = root.TryGetProperty("LearnerId", out var lId) ? lId.GetInt32() : 0;
                        var mentorId = root.TryGetProperty("MentorId", out var mId) ? mId.GetInt32() : 0;

                        if (learnerId > 0)
                        {
                            await DispatchNotificationAsync(
                                learnerId,
                                "Đặt lịch tư vấn thành công",
                                "Lịch tư vấn của bạn đã được xác nhận.",
                                "MentorBooking",
                                $"/mentor/bookings/{bookingId}",
                                cancellationToken);
                        }

                        if (mentorId > 0)
                        {
                            await DispatchNotificationAsync(
                                mentorId,
                                "Lịch tư vấn mới",
                                "Bạn có lịch tư vấn mới từ học viên.",
                                "MentorBooking",
                                $"/mentor/meetings/{bookingId}",
                                cancellationToken);
                        }
                        break;
                    }

                    case "BookingReleased":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var learnerId = root.TryGetProperty("LearnerId", out var lId) ? lId.GetInt32() : 0;
                        var mentorId = root.TryGetProperty("MentorId", out var mId) ? mId.GetInt32() : 0;

                        if (learnerId > 0)
                        {
                            await DispatchNotificationAsync(
                                learnerId,
                                "Khung giờ tư vấn đã mở lại",
                                "Khung giờ của buổi tư vấn đã được mở lại. Vui lòng xem chi tiết lịch tư vấn.",
                                "MentorBooking",
                                $"/mentor/bookings/{bookingId}",
                                cancellationToken);
                        }

                        if (mentorId > 0)
                        {
                            await DispatchNotificationAsync(
                                mentorId,
                                "Khung giờ tư vấn đã mở lại",
                                "Khung giờ của buổi tư vấn đã được mở lại. Vui lòng xem lịch của bạn.",
                                "MentorBooking",
                                $"/mentor/meetings/{bookingId}",
                                cancellationToken);
                        }
                        break;
                    }

                    case "BookingCancelled":
                    case "BookingLateCancelled":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var learnerId = root.TryGetProperty("LearnerId", out var lId) ? lId.GetInt32() : 0;
                        var mentorId = root.TryGetProperty("MentorId", out var mId) ? mId.GetInt32() : 0;
                        var isLate = root.TryGetProperty("IsLate", out var late) && late.GetBoolean();

                        if (mentorId > 0)
                        {
                            await DispatchNotificationAsync(
                                mentorId,
                                "Lịch tư vấn đã bị hủy",
                                $"Buổi tư vấn đã được học viên hủy{(isLate ? " (trong vòng 24 giờ trước giờ bắt đầu)" : "")}.",
                                "MentorBooking",
                                $"/mentor/meetings/{bookingId}",
                                cancellationToken);
                        }

                        if (learnerId > 0)
                        {
                            await DispatchNotificationAsync(
                                learnerId,
                                "Xác nhận hủy lịch tư vấn",
                                $"Bạn đã hủy buổi tư vấn{(isLate ? " trong vòng 24 giờ trước giờ bắt đầu; vé tư vấn không được hoàn" : "; vé tư vấn đã được hoàn")}.",
                                "MentorBooking",
                                $"/mentor/bookings/{bookingId}",
                                cancellationToken);
                        }
                        break;
                    }

                    case "BookingCancelledByMentor":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var learnerId = root.TryGetProperty("LearnerId", out var lId) ? lId.GetInt32() : 0;

                        if (learnerId > 0)
                        {
                            await DispatchNotificationAsync(
                                learnerId,
                                "Cố vấn đã hủy lịch tư vấn",
                                "Cố vấn đã hủy buổi tư vấn. Vé tư vấn đã được hoàn vào tài khoản của bạn.",
                                "MentorBooking",
                                $"/mentor/bookings/{bookingId}",
                                cancellationToken);
                        }
                        break;
                    }

                    case "BookingRescheduled":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var learnerId = root.TryGetProperty("LearnerId", out var lId) ? lId.GetInt32() : 0;
                        var mentorId = root.TryGetProperty("MentorId", out var mId) ? mId.GetInt32() : 0;

                        if (mentorId > 0)
                        {
                            await DispatchNotificationAsync(
                                mentorId,
                                "Lịch tư vấn được đổi khung giờ",
                                "Buổi tư vấn đã được đổi sang khung giờ mới.",
                                "MentorBooking",
                                "/mentor/schedule",
                                cancellationToken);
                        }

                        if (learnerId > 0)
                        {
                            await DispatchNotificationAsync(
                                learnerId,
                                "Đổi lịch tư vấn thành công",
                                "Buổi tư vấn của bạn đã được chuyển sang khung giờ mới.",
                                "MentorBooking",
                                $"/mentor/bookings/{bookingId}",
                                cancellationToken);
                        }
                        break;
                    }

                    case "ReviewSubmitted":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var mentorId = root.TryGetProperty("MentorId", out var mId) ? mId.GetInt32() : 0;
                        var rating = root.TryGetProperty("Rating", out var r) ? r.GetInt32() : 5;

                        if (mentorId > 0)
                        {
                            await DispatchNotificationAsync(
                                mentorId,
                                "Đánh giá mới từ học viên",
                                $"Bạn nhận được đánh giá {rating} sao cho một buổi tư vấn.",
                                "MentorReview",
                                "/mentor/reviews",
                                cancellationToken);
                        }
                        break;
                    }

                    case "DisputeSubmitted":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var mentorId = root.TryGetProperty("MentorId", out var mId) ? mId.GetInt32() : 0;

                        if (mentorId > 0)
                        {
                            await DispatchNotificationAsync(
                                mentorId,
                                "Khiếu nại về buổi tư vấn",
                                "Học viên đã gửi khiếu nại về một buổi tư vấn. Ban quản trị đang xử lý.",
                                "MentorDispute",
                                "/mentor/disputes",
                                cancellationToken);
                        }
                        break;
                    }

                    case "DisputeResolved":
                    {
                        var bookingId = root.TryGetProperty("BookingId", out var bId) ? bId.GetInt32() : 0;
                        var learnerId = root.TryGetProperty("LearnerId", out var lId) ? lId.GetInt32() : 0;
                        var mentorId = root.TryGetProperty("MentorId", out var mId) ? mId.GetInt32() : 0;

                        if (learnerId > 0)
                        {
                            await DispatchNotificationAsync(
                                learnerId,
                                "Kết quả xử lý khiếu nại",
                                "Khiếu nại về buổi tư vấn của bạn đã được giải quyết.",
                                "MentorDispute",
                                $"/mentor/bookings/{bookingId}",
                                cancellationToken);
                        }

                        if (mentorId > 0)
                        {
                            await DispatchNotificationAsync(
                                mentorId,
                                "Kết quả xử lý khiếu nại",
                                "Ban quản trị đã đưa ra quyết định về khiếu nại liên quan đến buổi tư vấn.",
                                "MentorDispute",
                                "/mentor/disputes",
                                cancellationToken);
                        }
                        break;
                    }
                }

                if (deferredBackoffSeconds > 0)
                {
                    evt.AvailableAtUtc = DateTime.UtcNow.AddSeconds(deferredBackoffSeconds);
                    evt.DeliveredAtUtc = null;
                    _logger.LogWarning("Outbox event {EventId} (type {EventType}) deferred to {AvailableAtUtc} due to FCM quota/backoff ({Seconds}s)",
                        evt.Id, evt.EventType, evt.AvailableAtUtc, deferredBackoffSeconds);
                }
                else
                {
                    evt.DeliveredAtUtc = DateTime.UtcNow;
                    processedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch outbox event {EventId} (type: {EventType}, attempt: {Attempts})",
                    evt.Id, evt.EventType, evt.Attempts);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return processedCount;
    }
}
