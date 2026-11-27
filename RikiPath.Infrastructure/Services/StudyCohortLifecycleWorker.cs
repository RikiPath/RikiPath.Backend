using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Services;

/// <summary>
/// Tự động hóa vòng đời giữ chỗ và tuyển sinh lớp học:
/// 1) Giải phóng giữ chỗ thanh toán đã quá hạn (sau khi đối soát PayOS để không làm mất giao dịch đã trả tiền).
/// 2) Tự động đóng tuyển sinh & hủy các lớp không có học viên khi hết hạn tuyển sinh, giải phóng lịch Giáo viên.
/// 3) Tự động đồng bộ các đề xuất đã phê duyệt chưa tạo lớp (nếu có).
/// </summary>
public sealed class StudyCohortLifecycleWorker(
    IServiceScopeFactory scopes,
    ILogger<StudyCohortLifecycleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        await RunPassAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunPassAsync(stoppingToken);
        }
    }

    private async Task RunPassAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nowUtc = DateTime.UtcNow;

            await ExpireStaleSeatHoldsAsync(scope.ServiceProvider, db, nowUtc, ct);
            await ProcessEnrollmentDeadlinesAsync(scope.ServiceProvider, ct);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StudyCohortLifecycleWorker pass encountered an unexpected error.");
        }
    }

    private async Task ExpireStaleSeatHoldsAsync(IServiceProvider sp, AppDbContext db, DateTime nowUtc, CancellationToken ct)
    {
        var expiredCourses = await db.StudyCourses
            .Where(c => c.PaymentStatus == "Pending"
                && c.PaymentExpiresAt.HasValue
                && c.PaymentExpiresAt.Value <= nowUtc)
            .OrderBy(c => c.Id)
            .Take(100)
            .ToListAsync(ct);

        if (expiredCourses.Count == 0)
            return;

        var paymentClient = sp.GetService<IPaymentGatewayClient>();
        List<int>? cachedAdminIds = null;

        foreach (var course in expiredCourses)
        {
            if (paymentClient is not null && course.Amount > 0)
            {
                var orderCode = 700_000_000L + course.Id;
                PaymentLinkInformation? info = null;
                try
                {
                    info = await paymentClient.GetPaymentInformationAsync(orderCode, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug(ex, "Lifecycle worker skipped PayOS status check for expired hold StudyCourse #{CourseId}.", course.Id);
                }

                var remoteStatus = info?.Status?.Trim().ToUpperInvariant();
                if (info is not null && remoteStatus == "PAID" && info.AmountPaid >= course.Amount)
                {
                    course.PaymentStatus = "Paid";
                    course.Status = "PaymentReview";
                    course.PaymentReviewReason = "RESERVATION_EXPIRED";
                    course.PaymentConfirmedAtUtc = nowUtc;
                    course.PaymentReference = info.PaymentLinkId;
                    if (!string.IsNullOrWhiteSpace(info.PaymentLinkId))
                        course.ProviderPaymentReference = info.PaymentLinkId.Length > 120 ? info.PaymentLinkId[..120] : info.PaymentLinkId;
                    course.Audit.Add(new CourseAuditEntry
                    {
                        CourseId = course.Id,
                        Action = "PaidEnrollmentUnresolved",
                        Detail = "Phát hiện thanh toán từ PayOS sau khi giữ chỗ hết hạn; chuyển Quản trị viên xem xét hoàn tiền hoặc chuyển lớp.",
                        At = nowUtc,
                    });

                    cachedAdminIds ??= await db.Set<UserAccount>().AsNoTracking()
                        .Where(u => u.Role == Role.Admin && !u.IsDeleted)
                        .Select(u => u.Id)
                        .ToListAsync(ct);

                    var notifications = new List<object>
                    {
                        new
                        {
                            UserId = course.LearnerId,
                            Title = "Giao dịch đang chờ xem xét",
                            Message = "Giao dịch đã được ghi nhận sau khi giữ chỗ hết hạn. Quản trị viên sẽ xem xét yêu cầu của bạn.",
                            Type = "PaymentReview",
                            LinkUrl = "/my-courses/",
                        }
                    };
                    foreach (var adminId in cachedAdminIds)
                    {
                        notifications.Add(new
                        {
                            UserId = adminId,
                            Title = "Đơn ghi danh cần xem xét thanh toán",
                            Message = "Một giao dịch ghi danh được thanh toán sau khi giữ chỗ hết hạn; vui lòng xem xét xử lý.",
                            Type = "PaymentReview",
                            LinkUrl = $"/admin/payments?stream=study_course&id={course.Id}",
                        });
                    }
                    db.MentorOutboxEvents.Add(new MentorOutboxEvent
                    {
                        EventKey = $"course-payment-review:{course.Id}",
                        AggregateType = "StudyCourse",
                        AggregateId = course.Id.ToString(CultureInfo.InvariantCulture),
                        EventType = "CohortLearningNotification",
                        EventVersion = 1,
                        PayloadJson = JsonSerializer.Serialize(new { Notifications = notifications }),
                        CreatedAtUtc = nowUtc,
                        AvailableAtUtc = nowUtc,
                    });
                    continue;
                }

                if (info is not null && remoteStatus == "PENDING")
                {
                    try
                    {
                        await paymentClient.CancelPaymentLinkAsync(orderCode, "Hết thời gian giữ chỗ thanh toán", ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogDebug(ex, "Lifecycle worker best-effort PayOS cancel failed for StudyCourse #{CourseId}.", course.Id);
                    }
                }
            }

            course.PaymentStatus = "Expired";
            course.Status = "PaymentExpired";
            course.Audit.Add(new CourseAuditEntry
            {
                CourseId = course.Id,
                Action = "SeatHoldExpired",
                Detail = "Hết thời gian giữ chỗ thanh toán; chỗ đã được tự động giải phóng.",
                At = nowUtc,
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Processed {Count} stale StudyCourse seat hold(s).", expiredCourses.Count);
    }

    private async Task ProcessEnrollmentDeadlinesAsync(IServiceProvider sp, CancellationToken ct)
    {
        var deadlineService = sp.GetService<ICohortEnrollmentDeadlineService>();
        if (deadlineService is null)
            return;

        var processed = await deadlineService.ProcessAllPastCutoffCohortsAsync(ct);
        if (processed > 0)
            logger.LogInformation("Processed enrollment deadlines for {Count} cohort(s).", processed);
    }
}
