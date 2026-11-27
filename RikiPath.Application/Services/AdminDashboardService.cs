using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class AdminDashboardService(IUnitOfWork unitOfWork, IClaimService claims) : IAdminDashboardService
{
    public async Task<ApiResponse<AdminDashboardResponse>> GetAsync(CancellationToken cancellationToken)
    {
        if (claims.GetUserClaim().Role != Role.Admin)
            return ApiResponse<AdminDashboardResponse>.Fail("Chỉ Quản trị viên mới được xem tổng quan hệ thống.", HttpStatusCode.Forbidden);

        var now = DateTime.UtcNow;
        // Keep queries sequential: repositories share one DbContext.
        var users = unitOfWork.UserAccounts.Query().Where(x => !x.IsDeleted);
        var totalUsers = await users.CountAsync(cancellationToken);
        var learners = await users.CountAsync(x => x.Role == Role.Learner, cancellationToken);
        var mentors = await users.CountAsync(x => x.Role == Role.Mentor, cancellationToken);
        var pendingContent = await unitOfWork.ContentApprovalRequests.Query()
            .CountAsync(x => !x.IsDeleted && x.Status == ContentApprovalStatus.PendingReview, cancellationToken);
        var pendingMentorSchedules = await unitOfWork.MentorAvailabilities.Query()
            .CountAsync(x => !x.IsDeleted && !x.IsApproved && x.RejectedAt == null && x.TeachingProposalId == null && x.StartTime > now, cancellationToken);
        var pendingTeachingProposals = await unitOfWork.MentorTeachingProposals.Query()
            .CountAsync(x => x.Status == MentorTeachingProposalStatus.PendingReview, cancellationToken);
        var pendingVerifications = await unitOfWork.MentorProfiles.Query()
            .CountAsync(x => !x.IsDeleted && x.VerificationStatus == MentorVerificationStatus.Pending
                && !x.Mentor.IsDeleted, cancellationToken);
        var pendingSlotCancelRequests = await unitOfWork.MentorAvailabilities.Query()
            .CountAsync(x => !x.IsDeleted && x.CancelRequestedAt != null && x.CancelRejectedAt == null && x.StartTime > now, cancellationToken);
        var pendingSlotChangeRequests = await unitOfWork.MentorAvailabilities.Query()
            .CountAsync(x => !x.IsDeleted && x.ChangeRequestedAt != null && x.ChangeResolution == null && x.StartTime > now, cancellationToken);
        var openComplaints = await unitOfWork.CohortComplaints.Query()
            .CountAsync(x => x.Status == CohortComplaint.Open || x.Status == CohortComplaint.UnderReview, cancellationToken);
        var pendingTransferRequests = await unitOfWork.CohortTransferRequests.Query()
            .CountAsync(x => x.Status == CohortTransferRequest.Pending, cancellationToken);
        var pendingCohortScheduleRequests = await unitOfWork.CohortScheduleRequests.Query()
            .CountAsync(x => x.Status == CohortScheduleRequest.Pending, cancellationToken);
        var publishedCohorts = await unitOfWork.StudyCohorts.Query()
            .CountAsync(x => x.Status == "Published", cancellationToken);
        var draftCohorts = await unitOfWork.StudyCohorts.Query()
            .CountAsync(x => x.Status == "Draft", cancellationToken);
        var paymentReviewCourses = await unitOfWork.StudyCourses.Query()
            .CountAsync(x => x.Status == "PaymentReview", cancellationToken);
        var bookings = await unitOfWork.MentorBookings.Query().CountAsync(x => !x.IsDeleted, cancellationToken);
        var plans = await unitOfWork.SubscriptionPlans.Query().CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);
        var paid = await unitOfWork.UserSubscriptions.Query()
            .CountAsync(x => !x.IsDeleted && x.PaymentStatus == PaymentStatus.Paid, cancellationToken);

        // Server-authoritative revenue summary across all monetary transactions (no 100-row client truncation)
        var subRows = await unitOfWork.UserSubscriptions.Query().AsNoTracking()
            .Where(x => !x.IsDeleted && (x.PaymentStatus == PaymentStatus.Paid || x.PaymentStatus == PaymentStatus.Pending || x.PaymentStatus == PaymentStatus.Refunded))
            .Select(x => new { x.AmountPaid, x.PaymentStatus, Anchor = x.StartDate ?? (x.PurchasedAt == default ? (x.CreatedDate ?? now) : x.PurchasedAt) })
            .ToListAsync(cancellationToken);

        var courseRows = await unitOfWork.StudyCourses.Query().AsNoTracking()
            .Include(x => x.CertificateLevel)
            .Where(x => x.Amount > 0 && x.RetakeOfId == null && x.TransferredFromCourseId == null)
            .Select(x => new
            {
                x.Amount,
                x.RefundedAmount,
                x.PaymentStatus,
                x.Status,
                x.PaymentExpiresAt,
                LevelCode = x.CertificateLevel != null ? x.CertificateLevel.Code : "Khác",
                Anchor = x.PaymentConfirmedAtUtc ?? x.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var topUpRows = await unitOfWork.AiCreditTopUps.Query().AsNoTracking()
            .Where(x => !x.IsDeleted && (x.PaymentStatus == PaymentStatus.Paid || x.PaymentStatus == PaymentStatus.Pending || x.PaymentStatus == PaymentStatus.Refunded))
            .Select(x => new { x.TotalAmount, x.PaymentStatus, Anchor = x.ActivatedAt ?? (x.CreatedDate ?? now) })
            .ToListAsync(cancellationToken);

        static decimal CourseNetCollected(decimal amount, decimal refundedAmount, string paymentStatus, string status)
            => status != "PaymentReview" && paymentStatus is "Paid" or "PartiallyRefunded"
                ? Math.Max(0m, amount - Math.Clamp(refundedAmount, 0m, amount))
                : 0m;

        static decimal CourseRefunded(decimal amount, decimal refundedAmount, string paymentStatus) => paymentStatus switch
        {
            "Refunded" => refundedAmount > 0 ? Math.Min(refundedAmount, amount) : amount,
            "PartiallyRefunded" => Math.Clamp(refundedAmount, 0m, amount),
            _ => 0m,
        };

        var subscriptionRevenue = subRows.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.AmountPaid);
        var courseTuitionRevenue = courseRows.Sum(x => CourseNetCollected(x.Amount, x.RefundedAmount, x.PaymentStatus, x.Status));
        var aiTopUpRevenue = topUpRows.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.TotalAmount);
        var totalPaidAmount = subscriptionRevenue + courseTuitionRevenue + aiTopUpRevenue;
        var totalRefundedAmount = subRows.Where(x => x.PaymentStatus == PaymentStatus.Refunded).Sum(x => x.AmountPaid)
            + courseRows.Sum(x => CourseRefunded(x.Amount, x.RefundedAmount, x.PaymentStatus))
            + topUpRows.Where(x => x.PaymentStatus == PaymentStatus.Refunded).Sum(x => x.TotalAmount);
        var paidCoursesCount = courseRows.Count(x => (x.PaymentStatus == "Paid" || x.PaymentStatus == "PartiallyRefunded") && x.Status != "PaymentReview");
        var pendingPaymentCount = subRows.Count(x => x.PaymentStatus == PaymentStatus.Pending)
            + topUpRows.Count(x => x.PaymentStatus == PaymentStatus.Pending)
            + courseRows.Count(x => x.PaymentStatus == "Pending" && (!x.PaymentExpiresAt.HasValue || x.PaymentExpiresAt.Value > now));

        var vnZone = TimeZoneInfo.FindSystemTimeZoneById(RikiPath.Domain.MentorScheduling.MentorSlotPolicy.BusinessTimeZoneId);
        var todayVn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, vnZone));
        var dailyMap = new Dictionary<DateOnly, (decimal Sub, decimal Course, decimal Ai, decimal Refund)>();
        for (var d = 29; d >= 0; d--)
            dailyMap[todayVn.AddDays(-d)] = (0m, 0m, 0m, 0m);

        DateOnly ToVnDay(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, vnZone));
        }

        foreach (var s in subRows)
        {
            var day = ToVnDay(s.Anchor);
            if (!dailyMap.TryGetValue(day, out var b)) continue;
            if (s.PaymentStatus == PaymentStatus.Paid) dailyMap[day] = (b.Sub + s.AmountPaid, b.Course, b.Ai, b.Refund);
            else if (s.PaymentStatus == PaymentStatus.Refunded) dailyMap[day] = (b.Sub, b.Course, b.Ai, b.Refund + s.AmountPaid);
        }
        foreach (var c in courseRows)
        {
            var day = ToVnDay(c.Anchor);
            if (!dailyMap.TryGetValue(day, out var b)) continue;
            var net = CourseNetCollected(c.Amount, c.RefundedAmount, c.PaymentStatus, c.Status);
            var refunded = CourseRefunded(c.Amount, c.RefundedAmount, c.PaymentStatus);
            if (net > 0m || refunded > 0m)
                dailyMap[day] = (b.Sub, b.Course + net, b.Ai, b.Refund + refunded);
        }
        foreach (var t in topUpRows)
        {
            var day = ToVnDay(t.Anchor);
            if (!dailyMap.TryGetValue(day, out var b)) continue;
            if (t.PaymentStatus == PaymentStatus.Paid) dailyMap[day] = (b.Sub, b.Course, b.Ai + t.TotalAmount, b.Refund);
            else if (t.PaymentStatus == PaymentStatus.Refunded) dailyMap[day] = (b.Sub, b.Course, b.Ai, b.Refund + t.TotalAmount);
        }

        var dailySeries = dailyMap
            .OrderBy(kv => kv.Key)
            .Select(kv => new AdminDashboardDailyRevenuePoint
            {
                Date = kv.Key.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                SubscriptionAmount = kv.Value.Sub,
                CourseTuitionAmount = kv.Value.Course,
                AiTopUpAmount = kv.Value.Ai,
                RefundedAmount = kv.Value.Refund,
                TotalPaidAmount = kv.Value.Sub + kv.Value.Course + kv.Value.Ai,
            })
            .ToList();

        var courseTuitionByLevel = courseRows
            .Where(x => (x.PaymentStatus == "Paid" || x.PaymentStatus == "PartiallyRefunded") && x.Status != "PaymentReview")
            .GroupBy(x => x.LevelCode)
            .OrderBy(g => g.Key)
            .Select(g => new AdminDashboardLevelTuitionItem
            {
                LevelCode = g.Key,
                EnrolledCount = g.Count(),
                TotalTuitionAmount = g.Sum(x => CourseNetCollected(x.Amount, x.RefundedAmount, x.PaymentStatus, x.Status)),
            })
            .ToList();

        return ApiResponse<AdminDashboardResponse>.Success(new AdminDashboardResponse
        {
            TotalUsers = totalUsers,
            Learners = learners,
            Mentors = mentors,
            PendingContent = pendingContent,
            PendingMentorSchedules = pendingMentorSchedules,
            PendingTeachingProposals = pendingTeachingProposals,
            PendingVerifications = pendingVerifications,
            PendingSlotCancelRequests = pendingSlotCancelRequests,
            PendingSlotChangeRequests = pendingSlotChangeRequests,
            OpenComplaints = openComplaints,
            PendingTransferRequests = pendingTransferRequests,
            PendingCohortScheduleRequests = pendingCohortScheduleRequests,
            PublishedCohorts = publishedCohorts,
            DraftCohorts = draftCohorts,
            PaymentReviewCourses = paymentReviewCourses,
            MentorBookings = bookings,
            ActivePlans = plans,
            PaidSubscriptions = paid,
            RevenueSummary = new AdminDashboardRevenueSummary
            {
                TotalPaidAmount = totalPaidAmount,
                SubscriptionRevenue = subscriptionRevenue,
                CourseTuitionRevenue = courseTuitionRevenue,
                AiTopUpRevenue = aiTopUpRevenue,
                TotalRefundedAmount = totalRefundedAmount,
                NetCollectedAmount = totalPaidAmount,
                PaidCoursesCount = paidCoursesCount,
                PendingPaymentCount = pendingPaymentCount,
                ReviewPaymentCount = paymentReviewCourses,
                DailySeries = dailySeries,
                CourseTuitionByLevel = courseTuitionByLevel,
            },
        });
    }
}
