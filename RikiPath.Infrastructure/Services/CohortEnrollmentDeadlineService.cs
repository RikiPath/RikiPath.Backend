//using System.Globalization;
//using System.Text.Json;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using RikiPath.Application;
//using RikiPath.Application.IClients;
//using RikiPath.Application.IServices;
//using RikiPath.Domain.Entities;
//using RikiPath.Domain.Enums;
//using RikiPath.Domain.MentorScheduling;

//namespace RikiPath.Infrastructure.Services;

//public sealed class CohortEnrollmentDeadlineService(
//    AppDbContext db,
//    IUnitOfWork unitOfWork,
//    IPaymentGatewayClient? paymentClient = null,
//    ISidebarCounterNotifier? sidebarNotifier = null,
//    ILogger<CohortEnrollmentDeadlineService>? logger = null) : ICohortEnrollmentDeadlineService
//{
//    private const string EmptyCohortCancelReason = "Không có học viên ghi danh khi hết hạn tuyển sinh.";
//    private static readonly Lazy<TimeZoneInfo> BusinessZone =
//        new(() => TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId));

//    public async Task<CohortDeadlineProcessResult> ProcessCohortIfPastCutoffAsync(int cohortId, CancellationToken ct = default)
//    {
//        if (cohortId <= 0)
//            return new CohortDeadlineProcessResult(false, "NotFound");

//        var nowUtc = DateTime.UtcNow;
//        var header = await db.StudyCohorts.AsNoTracking()
//            .Include(c => c.GroupPolicy)
//            .Include(c => c.Sessions).ThenInclude(s => s.Availability).ThenInclude(a => a.GroupPolicy)
//            .AsSplitQuery()
//            .FirstOrDefaultAsync(c => c.Id == cohortId, ct);

//        if (header is null)
//            return new CohortDeadlineProcessResult(false, "NotFound");

//        if (string.Equals(header.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
//            return new CohortDeadlineProcessResult(false, "AlreadyCancelled", header.CancellationReason);

//        if (!string.Equals(header.Status, "Published", StringComparison.OrdinalIgnoreCase))
//            return new CohortDeadlineProcessResult(false, "NotPublished");

//        var effectivePolicy = header.GroupPolicy is null
//            ? await db.MentorGroupPolicies.AsNoTracking()
//                .Where(p => p.IsActive && !p.IsArchivedRevision && !p.IsDeleted)
//                .OrderByDescending(p => p.LastAppliedAtUtc)
//                .ThenByDescending(p => p.Version)
//                .FirstOrDefaultAsync(ct)
//            : null;

//        var cutoffUtc = ResolveCutoffUtc(header, effectivePolicy);
//        if (cutoffUtc > nowUtc)
//            return new CohortDeadlineProcessResult(false, "OpenForEnrollment");

//        return await ProcessLockedCohortAsync(cohortId, nowUtc, ct);
//    }

//    public async Task<int> ProcessAllPastCutoffCohortsAsync(CancellationToken ct = default)
//    {
//        var nowUtc = DateTime.UtcNow;
//        var effectivePolicy = await db.MentorGroupPolicies.AsNoTracking()
//            .Where(p => p.IsActive && !p.IsArchivedRevision && !p.IsDeleted)
//            .OrderByDescending(p => p.LastAppliedAtUtc)
//            .ThenByDescending(p => p.Version)
//            .FirstOrDefaultAsync(ct);

//        var candidates = await db.StudyCohorts.AsNoTracking()
//            .Include(c => c.GroupPolicy)
//            .Include(c => c.Sessions).ThenInclude(s => s.Availability).ThenInclude(a => a.GroupPolicy)
//            .AsSplitQuery()
//            .Where(c => c.Status == "Published"
//                && ((c.EnrollmentClosesAtUtc != null && c.EnrollmentClosesAtUtc <= nowUtc)
//                    || (c.EnrollmentClosesAtUtc == null && c.StartsAt <= nowUtc)))
//            .OrderBy(c => c.EnrollmentClosesAtUtc ?? c.StartsAt)
//            .Take(100)
//            .ToListAsync(ct);

//        var processedCount = 0;
//        foreach (var candidate in candidates)
//        {
//            var cutoffUtc = ResolveCutoffUtc(candidate, effectivePolicy);
//            if (cutoffUtc > nowUtc)
//                continue;

//            var res = await ProcessLockedCohortAsync(candidate.Id, nowUtc, ct);
//            if (res.Processed)
//                processedCount++;
//        }

//        return processedCount;
//    }

//    private async Task<CohortDeadlineProcessResult> ProcessLockedCohortAsync(int cohortId, DateTime nowUtc, CancellationToken ct)
//    {
//        CohortDeadlineProcessResult outcome = new(false, "NoOp");
//        var notifyUserIds = new List<int>();
//        var cancelledCohort = false;

//        await unitOfWork.ExecuteInTransactionAsync(async token =>
//        {
//            var mentorId = await db.StudyCohorts.AsNoTracking()
//                .Where(c => c.Id == cohortId)
//                .Select(c => (int?)c.MentorId)
//                .FirstOrDefaultAsync(token);
//            if (!mentorId.HasValue)
//            {
//                outcome = new CohortDeadlineProcessResult(false, "NotFound");
//                return;
//            }

//            // Tuần tự hóa khóa lớp trước, khóa lịch Giáo viên sau để không xung đột đồng thời với webhook thanh toán hoặc duyệt đề xuất.
//            await unitOfWork.StudyCourses.LockCohortAsync(cohortId, token);
//            await unitOfWork.StudyCourses.LockScheduleAsync(mentorId.Value, mentorId.Value, token);

//            var cohort = await db.StudyCohorts
//                .Include(c => c.GroupPolicy)
//                .Include(c => c.Sessions).ThenInclude(s => s.Availability).ThenInclude(a => a.GroupPolicy)
//                .Include(c => c.Enrollments).ThenInclude(e => e.Audit)
//                .AsSplitQuery()
//                .FirstOrDefaultAsync(c => c.Id == cohortId, token);

//            if (cohort is null)
//            {
//                outcome = new CohortDeadlineProcessResult(false, "NotFound");
//                return;
//            }

//            if (string.Equals(cohort.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
//            {
//                outcome = new CohortDeadlineProcessResult(false, "AlreadyCancelled", cohort.CancellationReason);
//                return;
//            }

//            if (!string.Equals(cohort.Status, "Published", StringComparison.OrdinalIgnoreCase))
//            {
//                outcome = new CohortDeadlineProcessResult(false, "NotPublished");
//                return;
//            }

//            var fallbackPolicy = cohort.GroupPolicy is null
//                ? await db.MentorGroupPolicies.AsNoTracking()
//                    .Where(p => p.IsActive && !p.IsArchivedRevision && !p.IsDeleted)
//                    .OrderByDescending(p => p.LastAppliedAtUtc)
//                    .ThenByDescending(p => p.Version)
//                    .FirstOrDefaultAsync(token)
//                : null;

//            var cutoffUtc = ResolveCutoffUtc(cohort, fallbackPolicy);
//            if (cutoffUtc > nowUtc)
//            {
//                outcome = new CohortDeadlineProcessResult(false, "OpenForEnrollment");
//                return;
//            }

//            // Bước 1: Kiểm tra và đối soát các phiên giữ chỗ Pending trước khi chốt sĩ số.
//            var pendingCourses = cohort.Enrollments
//                .Where(e => e.PaymentStatus == "Pending" && e.Status == "PendingPayment")
//                .ToList();

//            var adminIds = await db.Set<UserAccount>().AsNoTracking()
//                .Where(u => u.Role == Role.Admin && !u.IsDeleted)
//                .Select(u => u.Id)
//                .ToListAsync(token);

//            foreach (var pending in pendingCourses)
//            {
//                if (pending.PaymentExpiresAt.HasValue && pending.PaymentExpiresAt.Value > nowUtc)
//                {
//                    // Có học viên đang giữ chỗ thanh toán hợp lệ chưa hết hạn -> chờ đối soát trước khi tự hủy lớp.
//                    var liveStatus = await TryQueryPayOsStatusAsync(pending.Id, token);
//                    if (liveStatus is not null
//                        && string.Equals(liveStatus.Status?.Trim(), "PAID", StringComparison.OrdinalIgnoreCase)
//                        && liveStatus.AmountPaid >= pending.Amount)
//                    {
//                        MarkPendingPaymentForAdminReview(pending, liveStatus.PaymentLinkId, nowUtc, adminIds);
//                        continue;
//                    }

//                    // Nếu không còn đủ thời gian giữ chỗ và chưa thanh toán, không xác nhận học viên.
//                    continue;
//                }

//                // Giữ chỗ đã hết hạn tại thời điểm đóng tuyển sinh: đối soát PayOS trước khi giải phóng.
//                if (pending.Amount > 0)
//                {
//                    var remote = await TryQueryPayOsStatusAsync(pending.Id, token);
//                    var remoteStatus = remote?.Status?.Trim().ToUpperInvariant();
//                    if (remote is not null && remoteStatus == "PAID" && remote.AmountPaid >= pending.Amount)
//                    {
//                        MarkPendingPaymentForAdminReview(pending, remote.PaymentLinkId, nowUtc, adminIds);
//                        continue;
//                    }
//                    if (remote is not null && remoteStatus == "PENDING" && paymentClient is not null)
//                    {
//                        try
//                        {
//                            await paymentClient.CancelPaymentLinkAsync(700_000_000L + pending.Id, "Hết hạn tuyển sinh lớp học", token);
//                        }
//                        catch (Exception ex) when (ex is not OperationCanceledException)
//                        {
//                            logger?.LogDebug(ex, "Best-effort PayOS cancel failed during cutoff closure for course #{CourseId}.", pending.Id);
//                        }
//                    }
//                }

//                pending.PaymentStatus = "Expired";
//                pending.Status = "PaymentExpired";
//                pending.Audit.Add(new CourseAuditEntry
//                {
//                    CourseId = pending.Id,
//                    Action = "SeatHoldExpired",
//                    Detail = "Phiên giữ chỗ thanh toán đã hết hạn khi lớp đóng tuyển sinh.",
//                    At = nowUtc,
//                });
//            }

//            // Đếm lại số học viên ghi danh chính thức đã xác minh thanh toán (không tính giữ chỗ).
//            var confirmedCount = cohort.Enrollments
//                .Where(e => (e.PaymentStatus is "Paid" or "PartiallyRefunded")
//                    && e.Status is "Ready" or "Active" or "Completed")
//                .Select(e => e.LearnerId)
//                .Distinct()
//                .Count();

//            var activeUnexpiredHolds = cohort.Enrollments.Count(e =>
//                e.PaymentStatus == "Pending"
//                && e.Status == "PendingPayment"
//                && e.PaymentExpiresAt.HasValue
//                && e.PaymentExpiresAt.Value > nowUtc);

//            if (confirmedCount >= 1)
//            {
//                // Lớp có ít nhất 1 học viên ghi danh hợp lệ -> đóng tuyển sinh, tiếp tục Sắp diễn ra / Đang học.
//                cohort.EnrollmentClosesAtUtc ??= cutoffUtc;
//                cohort.EnrollmentClosedAtUtc ??= nowUtc;

//                var eventKey = $"cohort-enrollment-closed:{cohort.Id}";
//                var alreadyNotified = await db.MentorOutboxEvents.AsNoTracking()
//                    .AnyAsync(e => e.EventKey == eventKey, token);

//                if (!alreadyNotified)
//                {
//                    var vnStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(cohort.StartsAt, DateTimeKind.Utc), BusinessZone.Value);
//                    db.MentorOutboxEvents.Add(new MentorOutboxEvent
//                    {
//                        EventKey = eventKey,
//                        AggregateType = "StudyCohort",
//                        AggregateId = cohort.Id.ToString(CultureInfo.InvariantCulture),
//                        EventType = "CohortLearningNotification",
//                        EventVersion = 1,
//                        PayloadJson = JsonSerializer.Serialize(new
//                        {
//                            Notifications = new[]
//                            {
//                                new
//                                {
//                                    UserId = cohort.MentorId,
//                                    Title = $"Lớp {cohort.Name} đã đóng tuyển sinh",
//                                    Message = $"Lớp \"{cohort.Name}\" đã đóng tuyển sinh với {confirmedCount} học viên ghi danh và sẽ bắt đầu vào ngày {vnStart:dd/MM/yyyy}.",
//                                    Type = "CohortLifecycle",
//                                    LinkUrl = $"/mentor-study-courses/{cohort.Id}/",
//                                },
//                            },
//                        }),
//                        CreatedAtUtc = nowUtc,
//                        AvailableAtUtc = nowUtc,
//                    });
//                    notifyUserIds.Add(cohort.MentorId);
//                }

//                await db.SaveChangesAsync(token);
//                outcome = new CohortDeadlineProcessResult(true, "ClosedWithLearners", ConfirmedCount: confirmedCount);
//                return;
//            }

//            if (activeUnexpiredHolds > 0)
//            {
//                // Vẫn còn giữ chỗ thanh toán sống chưa hết hạn: lưu kết quả đối soát và đợi giữ chỗ hoàn tất hoặc hết hạn.
//                await db.SaveChangesAsync(token);
//                outcome = new CohortDeadlineProcessResult(false, "DeferredActiveHolds");
//                return;
//            }

//            // Không có học viên ghi danh hợp lệ khi hết hạn tuyển sinh -> tự động hủy lớp và giải phóng lịch dạy.
//            cohort.Status = "Cancelled";
//            cohort.CancellationReason = EmptyCohortCancelReason;
//            cohort.CancelledAtUtc = nowUtc;
//            cohort.EnrollmentClosesAtUtc ??= cutoffUtc;
//            cohort.EnrollmentClosedAtUtc ??= nowUtc;
//            cohort.Revision++;

//            foreach (var session in cohort.Sessions)
//            {
//                if (session.Availability is not null && !session.Availability.IsDeleted && session.Availability.StartTime > nowUtc.AddMinutes(-15))
//                {
//                    session.Availability.IsDeleted = true;
//                    session.Availability.ModifiedDate = nowUtc;
//                    session.Availability.ChangeResolution = EmptyCohortCancelReason;
//                }
//            }

//            var cancelEventKey = $"cohort-auto-cancelled-empty:{cohort.Id}";
//            var cancelNotified = await db.MentorOutboxEvents.AsNoTracking()
//                .AnyAsync(e => e.EventKey == cancelEventKey, token);

//            if (!cancelNotified)
//            {
//                var notifs = new List<object>
//                {
//                    new
//                    {
//                        UserId = cohort.MentorId,
//                        Title = "Lớp học đã được hủy",
//                        Message = $"Lớp \"{cohort.Name}\" đã được hủy do không có học viên ghi danh.",
//                        Type = "CohortLifecycle",
//                        LinkUrl = $"/mentor-study-courses/{cohort.Id}/",
//                    },
//                };
//                notifyUserIds.Add(cohort.MentorId);

//                foreach (var adminId in adminIds)
//                {
//                    notifs.Add(new
//                    {
//                        UserId = adminId,
//                        Title = "Lớp học tự động hủy khi hết hạn tuyển sinh",
//                        Message = $"Lớp \"{cohort.Name}\" đã được hủy do không có học viên ghi danh khi hết hạn tuyển sinh.",
//                        Type = "CohortLifecycle",
//                        LinkUrl = $"/admin/study-courses/?cohortId={cohort.Id}",
//                    });
//                    notifyUserIds.Add(adminId);
//                }

//                var expiredLearnerIds = cohort.Enrollments
//                    .Select(e => e.LearnerId)
//                    .Distinct()
//                    .ToList();
//                foreach (var learnerId in expiredLearnerIds)
//                {
//                    notifs.Add(new
//                    {
//                        UserId = learnerId,
//                        Title = "Lớp học đã ngừng mở",
//                        Message = $"Lớp \"{cohort.Name}\" đã được hủy do không có học viên ghi danh khi hết hạn tuyển sinh.",
//                        Type = "CohortLifecycle",
//                        LinkUrl = "/study-courses/",
//                    });
//                    notifyUserIds.Add(learnerId);
//                }

//                db.MentorOutboxEvents.Add(new MentorOutboxEvent
//                {
//                    EventKey = cancelEventKey,
//                    AggregateType = "StudyCohort",
//                    AggregateId = cohort.Id.ToString(CultureInfo.InvariantCulture),
//                    EventType = "CohortLearningNotification",
//                    EventVersion = 1,
//                    PayloadJson = JsonSerializer.Serialize(new { Notifications = notifs }),
//                    CreatedAtUtc = nowUtc,
//                    AvailableAtUtc = nowUtc,
//                });
//            }

//            db.AdminAuditLogs.Add(new AdminAuditLog
//            {
//                EntityType = "StudyCohort",
//                EntityId = cohort.Id,
//                Action = "AutoCancelledEmptyAtCutoff",
//                AfterJson = JsonSerializer.Serialize(new
//                {
//                    cohort.Id,
//                    cohort.Name,
//                    cohort.Status,
//                    cohort.CancellationReason,
//                    cohort.CancelledAtUtc,
//                }),
//                CreatedAtUtc = nowUtc,
//            });

//            await db.SaveChangesAsync(token);
//            cancelledCohort = true;
//            outcome = new CohortDeadlineProcessResult(true, "AutoCancelledEmpty", EmptyCohortCancelReason, 0);
//        }, ct);

//        if (sidebarNotifier is not null && (cancelledCohort || notifyUserIds.Count > 0))
//        {
//            await sidebarNotifier.NotifyAdminsAsync("cohort_lifecycle_updated", ct);
//            if (notifyUserIds.Count > 0)
//                await sidebarNotifier.NotifyUsersAsync(notifyUserIds.Distinct().ToList(), "cohort_lifecycle_updated", ct);
//        }

//        return outcome;
//    }

//    private async Task<PaymentLinkInformation?> TryQueryPayOsStatusAsync(int courseId, CancellationToken ct)
//    {
//        if (paymentClient is null)
//            return null;
//        try
//        {
//            return await paymentClient.GetPaymentInformationAsync(700_000_000L + courseId, ct);
//        }
//        catch (Exception ex) when (ex is not OperationCanceledException)
//        {
//            logger?.LogDebug(ex, "PayOS verification lookup failed for StudyCourse #{CourseId}.", courseId);
//            return null;
//        }
//    }

//    private void MarkPendingPaymentForAdminReview(
//        StudyCourse course,
//        string? paymentLinkId,
//        DateTime nowUtc,
//        IReadOnlyCollection<int> adminIds)
//    {
//        course.PaymentStatus = "Paid";
//        course.Status = "PaymentReview";
//        course.PaymentReviewReason = "ENROLLMENT_CUTOFF_PASSED";
//        course.PaymentConfirmedAtUtc = nowUtc;
//        if (!string.IsNullOrWhiteSpace(paymentLinkId))
//        {
//            course.PaymentReference = paymentLinkId;
//            course.ProviderPaymentReference = paymentLinkId.Length > 120 ? paymentLinkId[..120] : paymentLinkId;
//        }
//        course.Audit.Add(new CourseAuditEntry
//        {
//            CourseId = course.Id,
//            Action = "PaidEnrollmentUnresolved",
//            Detail = "Thanh toán được ghi nhận sau khi đóng tuyển sinh; chuyển Quản trị viên xem xét hoàn tiền hoặc chuyển lớp.",
//            At = nowUtc,
//        });

//        var reviewNotifs = new List<object>
//        {
//            new
//            {
//                UserId = course.LearnerId,
//                Title = "Giao dịch đang chờ xem xét",
//                Message = "Giao dịch thanh toán của bạn đã được ghi nhận sau thời điểm đóng tuyển sinh. Quản trị viên sẽ liên hệ hỗ trợ xử lý.",
//                Type = "PaymentReview",
//                LinkUrl = "/my-courses/",
//            },
//        };
//        foreach (var adminId in adminIds)
//        {
//            reviewNotifs.Add(new
//            {
//                UserId = adminId,
//                Title = "Giao dịch ghi danh cần xem xét",
//                Message = $"Có giao dịch thanh toán muộn khi đóng tuyển sinh lớp học. Vui lòng xem xét hoàn tiền hoặc chuyển lớp.",
//                Type = "PaymentReview",
//                LinkUrl = $"/admin/payments?stream=study_course&id={course.Id}",
//            });
//        }

//        db.MentorOutboxEvents.Add(new MentorOutboxEvent
//        {
//            EventKey = $"course-payment-review-cutoff:{course.Id}",
//            AggregateType = "StudyCourse",
//            AggregateId = course.Id.ToString(CultureInfo.InvariantCulture),
//            EventType = "CohortLearningNotification",
//            EventVersion = 1,
//            PayloadJson = JsonSerializer.Serialize(new { Notifications = reviewNotifs }),
//            CreatedAtUtc = nowUtc,
//            AvailableAtUtc = nowUtc,
//        });
//    }

//    private static DateTime ResolveCutoffUtc(StudyCohort cohort, MentorGroupPolicy? _)
//    {
//        if (cohort.EnrollmentClosesAtUtc.HasValue)
//            return DateTime.SpecifyKind(cohort.EnrollmentClosesAtUtc.Value, DateTimeKind.Utc);

//        var firstApproved = cohort.Sessions
//            .Where(s => s.Availability is not null && s.Availability.IsApproved && !s.Availability.IsDeleted && s.Availability.RejectedAt == null)
//            .OrderBy(s => s.Availability.StartTime)
//            .FirstOrDefault();

//        return firstApproved is null
//            ? DateTime.SpecifyKind(cohort.StartsAt, DateTimeKind.Utc)
//            : DateTime.SpecifyKind(firstApproved.Availability.StartTime, DateTimeKind.Utc);
//    }
//}
