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
/// Tự động hóa vòng đời giữ chỗ và tuyển sinh lớp học nhóm:
/// 1) Giải phóng giữ chỗ thanh toán đã quá hạn (PaymentStatus = Pending &amp;&amp; PaymentExpiresAt &lt;= nowUtc) thành Expired / PaymentExpired.
/// 2) Cảnh báo Quản trị viên một lần duy nhất (idempotent EventKey) khi lớp Published đã qua mốc đóng đăng ký nhưng chưa có học viên thanh toán hợp lệ.
/// 3) Tự động đồng bộ an toàn, chống trùng lặp các đề xuất đã phê duyệt nhưng chưa được khởi tạo lớp học nhóm.
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
            await NotifyEmptyCohortsPastCutoffAsync(db, nowUtc, ct);
            await BackfillApprovedProposalCohortsAsync(scope.ServiceProvider, ct);
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
                        Detail = "Phát hiện thanh toán từ PayOS sau khi giữ chỗ hết hạn; chuyển Quản trị viên xem xét hoàn tiền hoặc chuyển lớp, không tự ghi danh vượt sĩ số.",
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
                            LinkUrl = "/my-courses",
                        }
                    };
                    foreach (var adminId in cachedAdminIds)
                    {
                        notifications.Add(new
                        {
                            UserId = adminId,
                            Title = "Đơn ghi danh cần xem xét thanh toán",
                            Message = $"Đơn khóa học #{course.Id} ghi nhận thanh toán sau khi giữ chỗ hết hạn; vui lòng xem xét.",
                            Type = "PaymentReview",
                            LinkUrl = $"/admin/study-courses?courseId={course.Id}",
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
                Detail = "Hết thời gian giữ chỗ thanh toán; chỗ đã được tự động giải phóng và phiên thanh toán hết hiệu lực.",
                At = nowUtc,
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Processed {Count} stale StudyCourse seat hold(s).", expiredCourses.Count);
    }

    private async Task NotifyEmptyCohortsPastCutoffAsync(AppDbContext db, DateTime nowUtc, CancellationToken ct)
    {
        var effectivePolicy = await db.MentorGroupPolicies.AsNoTracking()
            .Where(p => p.IsActive && !p.IsArchivedRevision && !p.IsDeleted)
            .OrderByDescending(p => p.LastAppliedAtUtc)
            .ThenByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);

        var windowStart = nowUtc.AddDays(-7);
        var windowEnd = nowUtc.AddDays(7);

        var candidates = await db.StudyCohorts.AsNoTracking()
            .Include(c => c.GroupPolicy)
            .Include(c => c.Sessions).ThenInclude(s => s.Availability).ThenInclude(a => a.GroupPolicy)
            .Include(c => c.Enrollments)
            .Where(c => c.Status == "Published" && c.StartsAt >= windowStart && c.StartsAt <= windowEnd)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return;

        var emptyPastCutoff = new List<StudyCohort>();
        foreach (var cohort in candidates)
        {
            var firstApproved = cohort.Sessions
                .Where(s => s.Availability.IsApproved && !s.Availability.IsDeleted && s.Availability.RejectedAt == null)
                .OrderBy(s => s.Availability.StartTime)
                .FirstOrDefault();

            var cutoffMinutes = firstApproved?.Availability.GroupPolicy?.EnrollmentCutoffMinutesBeforeFirstSession
                ?? cohort.GroupPolicy?.EnrollmentCutoffMinutesBeforeFirstSession
                ?? effectivePolicy?.EnrollmentCutoffMinutesBeforeFirstSession
                ?? 0;
            var cutoffUtc = firstApproved is null
                ? DateTime.SpecifyKind(cohort.StartsAt, DateTimeKind.Utc)
                : DateTime.SpecifyKind(firstApproved.Availability.StartTime, DateTimeKind.Utc).AddMinutes(-cutoffMinutes);

            if (cutoffUtc > nowUtc)
                continue;

            var minLearnersToStart = cohort.GroupPolicy?.MinLearnersToStart
                ?? firstApproved?.Availability.GroupPolicy?.MinLearnersToStart
                ?? effectivePolicy?.MinLearnersToStart
                ?? 1;

            var confirmedCount = cohort.Enrollments.Count(e =>
                (e.PaymentStatus is "Paid" or "PartiallyRefunded")
                && e.Status != "PaymentReview"
                && e.Status != "Cancelled"
                && e.Status != "Transferred");

            if (confirmedCount < minLearnersToStart)
                emptyPastCutoff.Add(cohort);
        }

        if (emptyPastCutoff.Count == 0)
            return;

        var adminIds = await db.Set<UserAccount>().AsNoTracking()
            .Where(u => u.Role == Role.Admin && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (adminIds.Count == 0)
            return;

        var eventKeys = emptyPastCutoff.Select(c => $"cohort-empty-cutoff:{c.Id}").ToList();
        var alreadyQueued = await db.MentorOutboxEvents.AsNoTracking()
            .Where(e => eventKeys.Contains(e.EventKey))
            .Select(e => e.EventKey)
            .ToListAsync(ct);
        var queuedSet = alreadyQueued.ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var cohort in emptyPastCutoff)
        {
            var eventKey = $"cohort-empty-cutoff:{cohort.Id}";
            if (queuedSet.Contains(eventKey))
                continue;

            var notifications = adminIds.Select(adminId => new
            {
                UserId = adminId,
                Title = "Lớp học chưa có học viên trước giờ bắt đầu",
                Message = $"Lớp \"{cohort.Name}\" đã qua mốc đóng đăng ký nhưng chưa có học viên thanh toán hợp lệ. Vui lòng xem xét đổi ngày bắt đầu hoặc hủy lớp.",
                Type = "CohortLifecycle",
                LinkUrl = $"/admin/study-courses/?cohortId={cohort.Id}",
            }).ToList();

            db.MentorOutboxEvents.Add(new MentorOutboxEvent
            {
                EventKey = eventKey,
                AggregateType = "StudyCohort",
                AggregateId = cohort.Id.ToString(CultureInfo.InvariantCulture),
                EventType = "CohortLearningNotification",
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new { Notifications = notifications }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc,
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Queued empty-cohort cutoff alerts for {Count} cohort(s).", added);
        }
    }

    private async Task BackfillApprovedProposalCohortsAsync(IServiceProvider sp, CancellationToken ct)
    {
        var proposalService = sp.GetService<IMentorTeachingProposalService>();
        if (proposalService is null)
            return;

        var result = await proposalService.BackfillApprovedProposalCohortsSystemAsync(ct);
        if (result.CreatedCount > 0)
        {
            logger.LogInformation(
                "Auto-synced {Count} approved proposal(s) into StudyCohort(s): {CohortIds}",
                result.CreatedCount,
                string.Join(", ", result.CreatedCohortIds));
        }
    }
}
