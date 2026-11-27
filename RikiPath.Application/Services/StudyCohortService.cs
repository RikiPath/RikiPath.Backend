using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class StudyCourseService
{
    // Ghi danh đã chuyển lớp ("Transferred") hoặc chưa kích hoạt / bị hoàn tất giải phóng chỗ ở lớp nguồn.
    private static bool ReservesSeat(StudyCourse c, DateTime now) => (c.PaymentStatus is "Paid" or "PartiallyRefunded"
            && c.Status != "PaymentReview"
            && c.Status != "Cancelled"
            && c.Status != "Transferred"
            && c.Status != "PendingPayment"
            && c.Status != "PaymentExpired"
            && c.Status != "PaymentFailed")
        || (c.PaymentStatus == "Pending" && c.PaymentExpiresAt > now);

    // Trạng thái thanh toán hiển thị dùng chung cho chi tiết khóa học và danh sách lớp.
    internal static string PaymentStateOf(StudyCourse c, DateTime now) => c.PaymentStatus switch
    {
        "Pending" => c.PaymentExpiresAt > now ? "AwaitingPayment" : "Expired",
        "Expired" => "Expired",
        "Failed" => "Failed",
        "Paid" => c.Status == "PaymentReview" ? "PaidUnderReview" : "Confirmed",
        "PartiallyRefunded" => "PartiallyRefunded",
        "Refunded" => "Refunded",
        "Cancelled" => "Cancelled",
        _ => c.PaymentStatus
    };

    private static DateTime EnrollmentCutoffUtc(StudyCohort cohort, MentorGroupPolicy? _ = null)
    {
        if (cohort.EnrollmentClosesAtUtc.HasValue)
            return cohort.EnrollmentClosesAtUtc.Value;

        var first = cohort.Sessions.Where(s => s.Availability.IsApproved && !s.Availability.IsDeleted
                && s.Availability.RejectedAt == null)
            .OrderBy(s => s.Availability.StartTime).FirstOrDefault();
        return first is null ? cohort.StartsAt : first.Availability.StartTime;
    }

    private async Task<bool> HasLearnerConflictAsync(int learnerId, IReadOnlyList<MentorAvailability> proposed, int excludeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var courses = await uow.StudyCourses.Query().Where(e => e.Id != excludeId && e.LearnerId == learnerId
            && e.Status != "Cancelled"
            && e.Status != "PaymentReview"
            && e.Status != "Transferred"
            && (e.PaymentStatus == "Paid" || e.PaymentStatus == "PartiallyRefunded" || (e.PaymentStatus == "Pending" && e.PaymentExpiresAt > now)))
            .Include(e => e.Sessions).Include(e => e.Cohort).ThenInclude(g => g!.Sessions).ThenInclude(s => s.Availability)
            .AsSplitQuery().ToListAsync(ct);
        foreach (var e in courses)
        {
            if (e.Sessions.Any(s => s.Status == "Scheduled" && proposed.Any(p =>
                s.ScheduledAt < p.EndTime && s.ScheduledAt.AddMinutes(e.SessionMinutes) > p.StartTime))) return true;
            if (e.PaymentStatus == "Pending" && e.Cohort is not null && e.Cohort.Sessions.Any(s => proposed.Any(p =>
                s.Availability.StartTime < p.EndTime && s.Availability.EndTime > p.StartTime))) return true;
        }
        return false;
    }

    public Task<ApiResponse<object>> CohortsAsync(CancellationToken ct) => CohortsAsync(null, ct);

    public Task<ApiResponse<object>> CohortsAsync(CohortDiscoveryFilter? filter, CancellationToken ct) => Run(async () =>
    {
        var now = DateTime.UtcNow;
        if (filter is not null)
        {
            Require(filter.Weekday is null or >= 0 and <= 6, "Thứ trong tuần phải từ 0 (Chủ nhật) đến 6 (Thứ 7).");
            Require(filter.SessionsPerWeek is null or >= 1 and <= 84, "Số buổi mỗi tuần phải là số nguyên dương hợp lệ.");
            Require(!filter.FromTime.HasValue || !filter.ToTime.HasValue || filter.FromTime <= filter.ToTime,
                "Giờ bắt đầu lọc phải trước hoặc bằng giờ kết thúc.");
        }
        if (Actor.Role == Role.Learner && enrollmentDeadlines is not null)
        {
            await enrollmentDeadlines.ProcessAllPastCutoffCohortsAsync(ct);
        }
        var mentorFilter = filter?.MentorId;
        var query = uow.StudyCohorts.Query().Include(x => x.Plan).ThenInclude(x => x.CertificateLevel)
            .Include(x => x.GroupPolicy)
            .Include(x => x.Mentor).Include(x => x.Sessions).ThenInclude(x => x.Availability).ThenInclude(x => x.GroupPolicy)
            .Include(x => x.Enrollments).ThenInclude(e => e.Sessions).AsSplitQuery()
            .Where(x => Actor.Role == Role.Admin || (Actor.Role == Role.Mentor && x.MentorId == Actor.Id)
                || (x.Status == "Published" && x.StartsAt > now && x.Plan.IsPublished));
        if (mentorFilter.HasValue) query = query.Where(x => x.MentorId == mentorFilter.Value);
        var rows = await query.OrderBy(x => x.StartsAt).ToListAsync(ct);
        if (filter is not null) rows = rows.Where(x => MatchesDiscoveryFilter(x, filter)).ToList();
        var policy = await EffectiveGroupPolicyAsync(now, ct);
        if (Actor.Role != Role.Admin && Actor.Role != Role.Mentor)
        {
            rows = rows.Where(x =>
            {
                if (!string.Equals(x.Status, "Published", StringComparison.OrdinalIgnoreCase)
                    || !x.Plan.IsPublished
                    || x.StartsAt <= now
                    || EnrollmentCutoffUtc(x, policy) <= now)
                {
                    return false;
                }
                var approvedStarts = x.Sessions
                    .Where(s => s.Availability is not null && s.Availability.IsApproved && !s.Availability.IsDeleted && s.Availability.RejectedAt == null)
                    .Select(s => s.Availability.StartTime)
                    .ToList();
                return approvedStarts.Count == 0 || approvedStarts.Min() > now;
            }).ToList();
        }
        var proposalIds = rows.Where(x => x.SourceProposalId.HasValue)
            .Select(x => x.SourceProposalId!.Value)
            .Distinct()
            .ToList();
        var linkedProposals = proposalIds.Count == 0
            ? new Dictionary<int, (string? CourseObjective, int CourseDurationWeeks, string? OptionalAttachmentsJson)>()
            : await uow.MentorTeachingProposals.Query().AsNoTracking()
                .Where(p => proposalIds.Contains(p.Id))
                .Select(p => new { p.Id, p.CourseObjective, p.CourseDurationWeeks, p.OptionalAttachmentsJson })
                .ToDictionaryAsync(p => p.Id, p => (p.CourseObjective, p.CourseDurationWeeks, p.OptionalAttachmentsJson), ct);
        var roomIds = rows
            .SelectMany(x => x.Sessions)
            .Select(s => s.Availability.RoomId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var attendanceByRoom = roomIds.Count == 0 || uow.MentorAttendanceRecords is null
            ? new Dictionary<Guid, List<MentorAttendanceRecord>>()
            : (await uow.MentorAttendanceRecords.Query().AsNoTracking()
                .Where(r => roomIds.Contains(r.RoomId))
                .ToListAsync(ct))
                .GroupBy(r => r.RoomId)
                .ToDictionary(g => g.Key, g => g.ToList());
        var mentorIds = rows.Select(x => x.MentorId).Distinct().ToList();
        var includeMentorWorkHistory = Actor.Role == Role.Mentor || Actor.Role == Role.Admin;
        var rescheduledHistoryRows = !includeMentorWorkHistory || mentorIds.Count == 0
            ? []
            : await uow.MentorAvailabilities.Query().AsNoTracking()
                .Where(a => mentorIds.Contains(a.MentorId)
                    && a.IsDeleted
                    && a.ChangeResolution != null
                    && a.ChangeResolution.Contains("sang khung giờ mới"))
                .OrderBy(a => a.StartTime)
                .Select(a => new
                {
                    a.Id,
                    a.MentorId,
                    a.TeachingProposalId,
                    a.StartTime,
                    a.EndTime,
                    a.ChangeResolution,
                })
                .ToListAsync(ct);
        var rescheduledHistoryByMentor = rescheduledHistoryRows
            .GroupBy(a => a.MentorId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var mentorRatings = mentorIds.Count == 0 || uow.CohortMentorReviews is null
            ? new Dictionary<int, (double AverageRating, int ReviewCount)>()
            : (await uow.CohortMentorReviews.Query().AsNoTracking()
                .Where(r => mentorIds.Contains(r.MentorId) && !r.IsHidden)
                .GroupBy(r => r.MentorId)
                .Select(g => new { MentorId = g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
                .ToListAsync(ct))
                .ToDictionary(x => x.MentorId, x => (AverageRating: Math.Round(x.Average, 2), ReviewCount: x.Count));
        return rows.Select(x =>
        {
            var effectiveCapacity = x.CapacitySnapshot is int snapCap && snapCap > 0 ? snapCap : x.Capacity;
            var confirmedEnrollments = x.Enrollments
                .Where(c => c.PaymentStatus is "Paid" or "PartiallyRefunded"
                    && c.Status is "Ready" or "Active" or "Completed")
                .GroupBy(c => c.LearnerId)
                .Select(g => g.OrderByDescending(c => c.Id).First())
                .ToList();
            var confirmedLearnerIds = confirmedEnrollments.Select(c => c.LearnerId).ToHashSet();
            var confirmedEnrolledCount = confirmedEnrollments.Count;
            var pendingHoldCount = x.Enrollments
                .Where(c => c.PaymentStatus == "Pending"
                    && c.Status == "PendingPayment"
                    && c.PaymentExpiresAt > now
                    && !confirmedLearnerIds.Contains(c.LearnerId))
                .Select(c => c.LearnerId)
                .Distinct()
                .Count();
            var enrolledCount = confirmedEnrolledCount + pendingHoldCount;
            var seatsAvailable = Math.Max(0, effectiveCapacity - enrolledCount);
            var isMentorView = Actor.Role == Role.Mentor;
            var effectivePrice = x.TotalTuitionSnapshot is decimal snap && snap > 0 ? snap : x.Plan.Price;
            var cohortPolicy = x.GroupPolicy
                ?? x.Sessions.Select(s => s.Availability.GroupPolicy).FirstOrDefault(p => p is not null)
                ?? policy;
            var hasMentorRating = mentorRatings.TryGetValue(x.MentorId, out var mentorRating);
            var hasAnyCompletedSession = confirmedEnrollments
                .SelectMany(c => c.Sessions)
                .Any(cs => cs.Status is "Present" or "Absent" or "ExcusedAbsent" or "Completed");
            var confirmedSessionsByAvailability = confirmedEnrollments
                .SelectMany(c => c.Sessions)
                .Where(cs => cs.AvailabilityId.HasValue)
                .GroupBy(cs => cs.AvailabilityId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Nguồn tài chính chính thức chỉ tổng hợp từ các giao dịch thanh toán thực tế của lớp (loại giữ chỗ, thất bại, hủy, miễn phí và trùng lặp).
            var monetaryOrders = x.Enrollments
                .Where(c => c.Amount > 0 && !c.RetakeOfId.HasValue && !c.TransferredFromCourseId.HasValue)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .ToList();
            var settledOrders = monetaryOrders
                .Where(c => c.Status != "PaymentReview"
                    && c.PaymentStatus is "Paid" or "PartiallyRefunded" or "Refunded")
                .ToList();
            var grossCollectedAmount = settledOrders.Sum(c => c.Amount);
            var totalRefundedAmount = settledOrders.Sum(c => c.PaymentStatus switch
            {
                "Refunded" => c.RefundedAmount > 0 ? Math.Min(c.RefundedAmount, c.Amount) : c.Amount,
                "PartiallyRefunded" => Math.Clamp(c.RefundedAmount, 0m, c.Amount),
                _ => 0m,
            });
            var netCollectedAmount = Math.Max(0m, grossCollectedAmount - totalRefundedAmount);
            var paidTransactionsCount = settledOrders.Count(c => c.PaymentStatus is "Paid" or "PartiallyRefunded");
            var refundedTransactionsCount = settledOrders.Count(c => c.PaymentStatus is "Refunded" or "PartiallyRefunded");

            var requiresEntitlement = x.Sessions.Any(s => s.Availability.GroupPolicyId.HasValue);
            var decision = !requiresEntitlement
                ? new GroupEnrollmentEntitlementDecision(true, RequiresCheckout: true)
                : (groupEntitlementContract ?? new GroupEnrollmentEntitlementContract())
                    .EvaluateGroupEnrollment(x, x.Plan, Actor.Id, null);
            var myEnrollment = Actor.Role == Role.Learner
                ? x.Enrollments.FirstOrDefault(c => c.LearnerId == Actor.Id && ReservesSeat(c, now))
                : null;
            var enrollmentClosesAtUtc = EnrollmentCutoffUtc(x, policy);
            var withinScheduleWindow = x.Status == "Published" && enrollmentClosesAtUtc > now
                && (!x.Sessions.Any() || x.Sessions.Min(s => s.Availability.StartTime) > now);
            var hasApprovedEntitlement = requiresEntitlement && decision.IsAuthorized && !decision.RequiresCheckout;
            var proposalMeta = x.SourceProposalId.HasValue
                && linkedProposals.TryGetValue(x.SourceProposalId.Value, out var foundProposalMeta)
                ? foundProposalMeta
                : default;
            var hasProposalMeta = x.SourceProposalId.HasValue
                && linkedProposals.ContainsKey(x.SourceProposalId.Value);
            var parsedAttachments = hasProposalMeta
                ? DeserializeCohortProposalAttachments(proposalMeta.OptionalAttachmentsJson)
                : [];
            var statusNormalized = x.Status?.Trim() ?? string.Empty;
            var isCohortActiveOrPublished = !string.Equals(statusNormalized, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(statusNormalized, "Draft", StringComparison.OrdinalIgnoreCase);
            var isEnrollmentClosed = string.Equals(statusNormalized, "Completed", StringComparison.OrdinalIgnoreCase)
                || hasAnyCompletedSession
                || enrollmentClosesAtUtc <= now
                || x.StartsAt <= now;
            var isWorkScheduleEligible = isCohortActiveOrPublished
                && confirmedEnrolledCount >= 1
                && isEnrollmentClosed;
            return new
            {
                x.Id,
                x.PlanId,
                x.Name,
                x.MentorId,
                MentorName = $"{x.Mentor.FirstName} {x.Mentor.LastName}".Trim(),
                MentorAvatarUrl = x.Mentor.AvatarUrl,
                MentorRatingAverage = hasMentorRating && mentorRating.ReviewCount > 0 ? mentorRating.AverageRating : (double?)null,
                MentorReviewCount = hasMentorRating ? mentorRating.ReviewCount : 0,
                x.CreatedAtUtc,
                x.StartsAt,
                x.EndsAt,
                Capacity = effectiveCapacity,
                x.CapacitySnapshot,
                x.PolicyVersionSnapshot,
                x.SourceProposalId,
                CourseObjective = hasProposalMeta ? proposalMeta.CourseObjective : null,
                CourseDurationWeeks = x.CourseDurationWeeks > 0
                    ? x.CourseDurationWeeks
                    : (hasProposalMeta && proposalMeta.CourseDurationWeeks > 0
                        ? proposalMeta.CourseDurationWeeks
                        : (x.Plan.SessionsPerWeek > 0 && x.Plan.TotalSessions is int planTotal && planTotal > 0
                            ? Math.Max(1, (int)Math.Ceiling((double)planTotal / x.Plan.SessionsPerWeek))
                            : 1)),
                CancellationReason = x.CancellationReason,
                CancelledAtUtc = x.CancelledAtUtc,
                Attachments = parsedAttachments
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                    .Select((a, idx) => new
                    {
                        a.Name,
                        Url = (string?)null,
                        a.ContentType,
                        a.SizeBytes,
                        Index = idx,
                    }).ToList(),
                x.Status,
                x.Revision,
                Level = x.Plan.CertificateLevel.Code,
                x.Plan.TotalSessions,
                x.Plan.SessionMinutes,
                x.Plan.SessionsPerWeek,
                TotalTeachingHours = TotalTeachingHoursOf(x.Plan.TotalSessions, x.Plan.SessionMinutes),
                SchedulePattern = SchedulePatternOf(x),
                EnrollmentClosesAtUtc = enrollmentClosesAtUtc,
                EnrollmentOpensAtUtc = x.EnrollmentOpensAtUtc,
                EnrollmentClosedAtUtc = x.EnrollmentClosedAtUtc,
                IsEnrollmentWindowOpen = withinScheduleWindow,
                IsFull = effectiveCapacity > 0 && confirmedEnrolledCount >= effectiveCapacity,
                IsAllSeatsHeld = effectiveCapacity > 0 && confirmedEnrolledCount < effectiveCapacity && seatsAvailable == 0 && pendingHoldCount > 0,
                EnrolledCount = enrolledCount,
                ConfirmedEnrolledCount = confirmedEnrolledCount,
                PendingHoldCount = pendingHoldCount,
                CheckoutHoldMinutes = cohortPolicy.CheckoutHoldMinutes,
                IsWorkScheduleEligible = isWorkScheduleEligible,
                RequiresEntitlement = requiresEntitlement,
                HasApprovedEntitlement = hasApprovedEntitlement,
                EntitlementReference = decision.IsAuthorized ? decision.EntitlementReference : null,
                // Thanh toán PayOS là đường ghi danh chuẩn; thiếu quyền Admin cấp không còn chặn ghi danh.
                EntitlementStatusMessage = (string?)null,
                CheckoutMode = hasApprovedEntitlement ? "AdminEntitlement" : "PayOS",
                MyEnrollmentCourseId = myEnrollment?.Id,
                MyEnrollmentStatus = myEnrollment?.Status,
                MyEnrollmentPaymentState = myEnrollment is null ? null : PaymentStateOf(myEnrollment, now),
                MyEnrollmentPaymentExpiresAtUtc = myEnrollment is not null && myEnrollment.PaymentStatus == "Pending" && myEnrollment.PaymentExpiresAt > now
                    ? myEnrollment.PaymentExpiresAt
                    : (DateTime?)null,
                EnrollmentOpen = withinScheduleWindow && seatsAvailable > 0 && myEnrollment is null,
                Price = effectivePrice,
                TuitionPerLearner = effectivePrice,
                TotalCollectedAmount = isMentorView ? 0m : netCollectedAmount,
                GrossCollectedAmount = isMentorView ? 0m : grossCollectedAmount,
                TotalRefundedAmount = isMentorView ? 0m : totalRefundedAmount,
                PaidTransactionsCount = isMentorView ? 0 : paidTransactionsCount,
                RefundedTransactionsCount = isMentorView ? 0 : refundedTransactionsCount,
                TuitionPerSession = x.TuitionPerSessionSnapshot,
                TuitionPolicyVersionSnapshot = x.TuitionPolicyVersionSnapshot,
                SeatsAvailable = seatsAvailable,
                Sessions = x.Sessions
                    .Where(s => s.Availability != null && s.Availability.IsApproved && !s.Availability.IsDeleted && s.Availability.RejectedAt == null)
                    .OrderBy(s => s.Number)
                    .Select(s =>
                    {
                        var roomRecords = s.Availability.RoomId != Guid.Empty && attendanceByRoom.TryGetValue(s.Availability.RoomId, out var recs)
                            ? recs
                            : [];
                        var mentorJoinedAtUtc = roomRecords
                            .Where(r => r.Role == "Mentor" && r.FirstJoinedAtUtc.HasValue)
                            .Select(r => r.FirstJoinedAtUtc)
                            .OrderBy(d => d)
                            .FirstOrDefault();
                        var sessionStartedAtUtc = mentorJoinedAtUtc
                            ?? roomRecords.Where(r => r.FirstJoinedAtUtc.HasValue)
                                .Select(r => r.FirstJoinedAtUtc)
                                .OrderBy(d => d)
                                .FirstOrDefault();
                        var slotConfirmedSessions = confirmedSessionsByAvailability.TryGetValue(s.AvailabilityId, out var matchedSessions)
                            ? matchedSessions
                            : [];
                        var sessionCompleted = slotConfirmedSessions.Count > 0
                            && slotConfirmedSessions.All(cs => cs.Status is "Present" or "Absent" or "ExcusedAbsent" or "Completed");
                        var inLiveWindow = now >= s.Availability.StartTime.AddMinutes(-15) && now <= s.Availability.EndTime;
                        var hasActiveParticipant = roomRecords.Any(r => r.Status == MentorAttendanceStatus.Joined);
                        var hasMentorReconnecting = roomRecords.Any(r =>
                            r.Role == "Mentor"
                            && r.Status == MentorAttendanceStatus.TemporarilyDisconnected
                            && r.LastSeenAtUtc.HasValue
                            && (now - r.LastSeenAtUtc.Value).TotalMinutes <= 10);
                        var isSessionLive = isWorkScheduleEligible
                            && !sessionCompleted
                            && inLiveWindow
                            && (hasActiveParticipant || hasMentorReconnecting);
                        return new
                        {
                            s.Number,
                            s.AvailabilityId,
                            s.Availability.RoomId,
                            ScheduledAt = s.Availability.StartTime,
                            s.Availability.StartTime,
                            s.Availability.EndTime,
                            SessionStartedAtUtc = sessionStartedAtUtc,
                            MentorJoinedAtUtc = mentorJoinedAtUtc,
                            IsSessionLive = isSessionLive,
                            SessionCompleted = sessionCompleted,
                        };
                    }),
                RescheduledHistorySessions = !includeMentorWorkHistory || string.Equals(statusNormalized, "Cancelled", StringComparison.OrdinalIgnoreCase)
                    ? Enumerable.Empty<object>()
                    : (rescheduledHistoryByMentor.TryGetValue(x.MentorId, out var mentorMovedSlots)
                        ? mentorMovedSlots
                            .Where(a => (x.SourceProposalId.HasValue && a.TeachingProposalId == x.SourceProposalId.Value)
                                || (!string.IsNullOrEmpty(a.ChangeResolution)
                                    && (a.ChangeResolution.Contains($"lớp #{x.Id} ")
                                        || a.ChangeResolution.Contains($"lớp #{x.Id})")
                                        || a.ChangeResolution.Contains($"của lớp {x.Name} sang khung giờ mới"))))
                            .Select(a => (object)new
                            {
                                AvailabilityId = a.Id,
                                StartTime = a.StartTime,
                                EndTime = a.EndTime,
                                ChangeResolution = a.ChangeResolution,
                            })
                        : Enumerable.Empty<object>())
            };
        }).ToList();
    });

    public async Task<ApiResponse<RikiPath.Application.Requests.MentorScheduling.ProposalAttachmentDto>> GetAuthorizedCohortAttachmentAsync(
        int cohortId, int attachmentIndex, CancellationToken ct)
    {
        var cohort = await uow.StudyCohorts.Query().AsNoTracking()
            .Where(x => x.Id == cohortId)
            .Select(x => new { x.Id, x.MentorId, x.Status, x.SourceProposalId })
            .FirstOrDefaultAsync(ct);
        if (cohort is null || !cohort.SourceProposalId.HasValue)
        {
            return ApiResponse<RikiPath.Application.Requests.MentorScheduling.ProposalAttachmentDto>.NotFound(
                "Không tìm thấy tài liệu của lớp học.", "COHORT_ATTACHMENT_NOT_FOUND");
        }

        var isAuthorized = Actor.Role switch
        {
            Role.Admin => true,
            Role.Mentor => cohort.MentorId == Actor.Id
                || cohort.Status is "Published" or "Completed",
            Role.Learner => cohort.Status is "Published" or "Completed"
                || await uow.StudyCourses.Query().AsNoTracking()
                    .AnyAsync(c => c.CohortId == cohortId && c.LearnerId == Actor.Id && c.Status != "Cancelled", ct),
            _ => false,
        };

        if (!isAuthorized)
        {
            return ApiResponse<RikiPath.Application.Requests.MentorScheduling.ProposalAttachmentDto>.Fail(
                "Bạn không có quyền truy cập tài liệu của lớp học này.",
                HttpStatusCode.Forbidden,
                "COHORT_ATTACHMENT_FORBIDDEN");
        }

        var json = await uow.MentorTeachingProposals.Query().AsNoTracking()
            .Where(p => p.Id == cohort.SourceProposalId.Value)
            .Select(p => p.OptionalAttachmentsJson)
            .FirstOrDefaultAsync(ct);
        var attachments = DeserializeCohortProposalAttachments(json)
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .ToList();

        if (attachmentIndex < 0 || attachmentIndex >= attachments.Count)
        {
            return ApiResponse<RikiPath.Application.Requests.MentorScheduling.ProposalAttachmentDto>.NotFound(
                "Không tìm thấy tài liệu đính kèm.", "COHORT_ATTACHMENT_NOT_FOUND");
        }

        return ApiResponse<RikiPath.Application.Requests.MentorScheduling.ProposalAttachmentDto>.Success(attachments[attachmentIndex]);
    }

    private static readonly System.Text.Json.JsonSerializerOptions CohortProposalJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static List<RikiPath.Application.Requests.MentorScheduling.ProposalAttachmentDto> DeserializeCohortProposalAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<RikiPath.Application.Requests.MentorScheduling.ProposalAttachmentDto>>(json, CohortProposalJsonOpts) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public Task<ApiResponse<object>> SaveCohortAsync(int? id, SaveStudyCohort r, CancellationToken ct) => Run(() =>
    {
        Admin();
        // Lớp học nhóm chỉ được tạo & công bố tự động khi phê duyệt đề xuất mở lớp; mọi điều chỉnh sau duyệt đi qua luồng chuyên biệt.
        if (!id.HasValue)
        {
            Rule(false, "LEGACY_MANUAL_COHORT_DISABLED",
                "Lớp học nhóm được tạo và mở tuyển sinh tự động khi phê duyệt đề xuất mở lớp; không hỗ trợ tạo lớp thủ công.",
                HttpStatusCode.Conflict);
        }
        else
        {
            Rule(false, "COHORT_SCHEDULE_LOCKED_BY_PROPOSAL",
                "Không thể chỉnh sửa trực tiếp gói học, giáo viên, sĩ số, lịch đã duyệt hoặc trạng thái mở lớp. Vui lòng sử dụng quy trình dời lịch, đổi giáo viên hoặc hủy lớp chuyên biệt.",
                HttpStatusCode.Conflict);
        }

        return Task.FromResult<object>(new { Id = id ?? 0 });
    });

    private static readonly Lazy<TimeZoneInfo> CohortBusinessZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById(RikiPath.Domain.MentorScheduling.MentorSlotPolicy.BusinessTimeZoneId));

    private static DateTime LocalTime(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(MentorTeachingConflictService.Utc(utc), CohortBusinessZone.Value);

    // Mẫu lịch học theo giờ Việt Nam: các cặp (thứ, giờ bắt đầu, giờ kết thúc) khác nhau.
    internal static string WeekdayLabelOf(DayOfWeek day) => day == DayOfWeek.Sunday ? "Chủ nhật" : $"Thứ {(int)day + 1}";

    private static List<CohortSchedulePatternItem> SchedulePatternOf(StudyCohort cohort)
        => cohort.Sessions
            .Select(s => (Start: LocalTime(s.Availability.StartTime), End: LocalTime(s.Availability.EndTime)))
            .Select(x => new CohortSchedulePatternItem((int)x.Start.DayOfWeek, WeekdayLabelOf(x.Start.DayOfWeek),
                x.Start.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                x.End.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)))
            .Distinct()
            .OrderBy(x => (x.Weekday + 6) % 7).ThenBy(x => x.LocalStart, StringComparer.Ordinal)
            .ToList();

    internal sealed record CohortSchedulePatternItem(int Weekday, string WeekdayLabel, string LocalStart, string LocalEnd);

    private static bool MatchesDiscoveryFilter(StudyCohort x, CohortDiscoveryFilter f)
    {
        if (f.MentorId.HasValue && x.MentorId != f.MentorId.Value) return false;
        if (!string.IsNullOrWhiteSpace(f.Level)
            && !string.Equals(x.Plan.CertificateLevel?.Code, f.Level.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (f.SessionsPerWeek.HasValue && x.Plan.SessionsPerWeek != f.SessionsPerWeek.Value) return false;
        if (f.Weekday.HasValue || f.FromTime.HasValue || f.ToTime.HasValue)
        {
            // Có ít nhất một buổi (giờ Việt Nam) đúng thứ được lọc và bắt đầu trong khung giờ [fromTime, toTime].
            var match = x.Sessions.Select(s => LocalTime(s.Availability.StartTime)).Any(s =>
            {
                var t = TimeOnly.FromDateTime(s);
                return (!f.Weekday.HasValue || (int)s.DayOfWeek == f.Weekday.Value)
                    && (!f.FromTime.HasValue || t >= f.FromTime.Value)
                    && (!f.ToTime.HasValue || t <= f.ToTime.Value);
            });
            if (!match) return false;
        }
        return true;
    }

    // Xem trước (Admin): báo cáo xung đột lịch dạy của Mentor cho các khung giờ đã chọn; không ghi dữ liệu.
    public Task<ApiResponse<object>> CohortSchedulePreviewAsync(CohortSchedulePreviewRequest r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Require(r is not null && r.MentorId > 0 && r.AvailabilityIds is { Count: > 0 and <= 500 }
            && r.AvailabilityIds.Distinct().Count() == r.AvailabilityIds.Count, "Vui lòng chọn Giáo viên và các khung giờ không trùng nhau.");
        var slots = await uow.MentorAvailabilities.Query().AsNoTracking()
            .Where(x => r!.AvailabilityIds.Contains(x.Id) && x.MentorId == r.MentorId && !x.IsDeleted && x.RejectedAt == null)
            .OrderBy(x => x.StartTime).Select(x => new { x.Id, x.StartTime, x.EndTime }).ToListAsync(ct);
        Require(slots.Count == r!.AvailabilityIds.Count, "Có khung giờ không tồn tại hoặc không thuộc Giáo viên đã chọn.");
        return await TeachingConflicts.CheckAsync(r.MentorId,
            slots.Select((s, i) => new ProposedTeachingInterval(s.StartTime, s.EndTime, i + 1)).ToList(),
            new TeachingConflictOptions { ExcludeCohortId = r.CohortId, ExcludeAvailabilityIds = r.AvailabilityIds }, ct);
    });

    private void AddCohortSessions(StudyCourse c, StudyCohort cohort)
    {
        c.MentorId = cohort.MentorId; c.StartsAt = cohort.StartsAt; c.EndsAt = cohort.EndsAt;
        c.Status = "Active";
        foreach (var s in cohort.Sessions)
            c.Sessions.Add(new() { Number = s.Number, ScheduledAt = s.Availability.StartTime,
                AvailabilityId = s.AvailabilityId, MeetingUrl = $"/meeting-room/{s.Availability.RoomId}" });
    }
}
