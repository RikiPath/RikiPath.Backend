using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorKpi;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Services;

/// <summary>
/// Tổng hợp chấm công, thời lượng giảng dạy và chỉ số KPI của Cố vấn từ dữ liệu thực tế (không tính lương/thù lao).
/// </summary>
public sealed class MentorKpiService(
    AppDbContext context,
    IClaimService claimService) : IMentorKpiService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<ApiResponse<MentorKpiSummaryResponse>> GetMyKpiSummaryAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var claim = claimService.GetUserClaim();
        if (claim.Role != Role.Mentor)
        {
            return ApiResponse<MentorKpiSummaryResponse>.Fail(
                "Chỉ Giáo viên mới được xem bảng Chấm công & KPI cá nhân.",
                HttpStatusCode.Forbidden);
        }

        var defaultPeriodDays = await ResolveReportingPeriodDaysAsync(cancellationToken);
        if (!TryResolveWindow(from, to, defaultPeriodDays, out var fromDate, out var toDate, out var error))
        {
            return ApiResponse<MentorKpiSummaryResponse>.Fail(error!, HttpStatusCode.BadRequest);
        }

        var mentor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == claim.Id && !u.IsDeleted && u.Role == Role.Mentor, cancellationToken);

        if (mentor is null)
        {
            return ApiResponse<MentorKpiSummaryResponse>.NotFound("Không tìm thấy hồ sơ Giáo viên.");
        }

        var summary = await BuildSummaryForMentorAsync(mentor, fromDate, toDate, cancellationToken);
        return ApiResponse<MentorKpiSummaryResponse>.Success(summary);
    }

    public async Task<ApiResponse<List<MentorKpiSummaryResponse>>> GetAdminMentorKpisAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        int? mentorId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var claim = claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
        {
            return ApiResponse<List<MentorKpiSummaryResponse>>.Fail(
                "Chỉ Quản trị viên mới có quyền xem báo cáo KPI toàn hệ thống.",
                HttpStatusCode.Forbidden);
        }

        var defaultPeriodDays = await ResolveReportingPeriodDaysAsync(cancellationToken);
        if (!TryResolveWindow(from, to, defaultPeriodDays, out var fromDate, out var toDate, out var error))
        {
            return ApiResponse<List<MentorKpiSummaryResponse>>.Fail(error!, HttpStatusCode.BadRequest);
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var mentorsQuery = context.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == Role.Mentor);

        if (mentorId.HasValue)
        {
            mentorsQuery = mentorsQuery.Where(u => u.Id == mentorId.Value);
        }

        var mentors = await mentorsQuery
            .OrderBy(u => u.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var results = new List<MentorKpiSummaryResponse>(mentors.Count);
        foreach (var mentor in mentors)
        {
            results.Add(await BuildSummaryForMentorAsync(mentor, fromDate, toDate, cancellationToken));
        }

        return ApiResponse<List<MentorKpiSummaryResponse>>.Success(results);
    }

    public async Task<ApiResponse<MentorKpiSummaryResponse>> GetAdminMentorKpiByMentorAsync(
        int mentorId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var claim = claimService.GetUserClaim();
        if (claim.Role != Role.Admin)
        {
            return ApiResponse<MentorKpiSummaryResponse>.Fail(
                "Chỉ Quản trị viên mới có quyền xem KPI chi tiết của Giáo viên.",
                HttpStatusCode.Forbidden);
        }

        var defaultPeriodDays = await ResolveReportingPeriodDaysAsync(cancellationToken);
        if (!TryResolveWindow(from, to, defaultPeriodDays, out var fromDate, out var toDate, out var error))
        {
            return ApiResponse<MentorKpiSummaryResponse>.Fail(error!, HttpStatusCode.BadRequest);
        }

        var mentor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == mentorId && !u.IsDeleted && u.Role == Role.Mentor, cancellationToken);

        if (mentor is null)
        {
            return ApiResponse<MentorKpiSummaryResponse>.NotFound("Không tìm thấy Giáo viên.");
        }

        var summary = await BuildSummaryForMentorAsync(mentor, fromDate, toDate, cancellationToken);
        return ApiResponse<MentorKpiSummaryResponse>.Success(summary);
    }

    private async Task<MentorKpiSummaryResponse> BuildSummaryForMentorAsync(
        UserAccount mentor,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var fromStartUtc = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);
        var toEndExclusiveUtc = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);

        var qualifications = await context.MentorTeachingQualifications
            .AsNoTracking()
            .Include(q => q.CertificateLevel)
            .Where(q => q.MentorUserId == mentor.Id && q.IsActive && q.RevokedAtUtc == null)
            .OrderBy(q => q.CertificateLevel.SortOrder)
            .Select(q => q.CertificateLevel.Code)
            .ToListAsync(cancellationToken);

        // StudyCourse & Cohort sessions
        var mentorCourses = await context.StudyCourses
            .AsNoTracking()
            .Include(c => c.CertificateLevel)
            .Include(c => c.Learner)
            .Include(c => c.Cohort)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Availability)
            .Include(c => c.Homework)
            .Where(c => (c.MentorId == mentor.Id || (c.Cohort != null && c.Cohort.MentorId == mentor.Id)
                    || c.Sessions.Any(s => s.Availability != null && s.Availability.MentorId == mentor.Id))
                && (c.Cohort == null || c.Cohort.Status != "Cancelled")
                && (c.PaymentStatus == "Paid" || c.PaymentStatus == "PartiallyRefunded")
                && (c.Status == "Active" || c.Status == "Completed" || c.Status == "Transferred"))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Mentor thực dạy của từng buổi là chủ khung giờ; sau khi thay Mentor chính, buổi cũ vẫn thuộc Mentor cũ.
        // Buổi "TransferredOut" (học viên đã chuyển lớp) không tính cho Mentor nào.
        var scopedCourseSessions = mentorCourses
            .SelectMany(c => c.Sessions.Select(s => new { Course = c, Session = s }))
            .Where(x => x.Session.ScheduledAt >= fromStartUtc && x.Session.ScheduledAt < toEndExclusiveUtc
                && x.Session.Status != "TransferredOut"
                && (x.Session.Availability?.MentorId ?? x.Course.MentorId ?? x.Course.Cohort?.MentorId) == mentor.Id)
            .ToList();

        var replacementCohortIds = mentorCourses.Where(c => c.CohortId.HasValue).Select(c => c.CohortId!.Value).Distinct().ToList();
        var mentorAssignments = replacementCohortIds.Count == 0
            ? new Dictionary<int, List<StudyCohortMentorAssignment>>()
            : (await context.StudyCohortMentorAssignments.AsNoTracking()
                .Where(a => replacementCohortIds.Contains(a.CohortId)).ToListAsync(cancellationToken))
                .GroupBy(a => a.CohortId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.EffectiveFromUtc).ThenBy(a => a.Id).ToList());
        int? MentorAt(StudyCourse course, DateTime atUtc)
        {
            if (!course.CohortId.HasValue || !mentorAssignments.TryGetValue(course.CohortId.Value, out var history) || history.Count == 0)
                return course.MentorId ?? course.Cohort?.MentorId;
            var current = history[0].PreviousMentorId;
            foreach (var a in history.Where(a => a.EffectiveFromUtc <= atUtc)) current = a.MentorId;
            return current;
        }

        var courseSessionIds = scopedCourseSessions.Select(x => x.Session.Id).ToList();
        var courseAttendanceSeconds = courseSessionIds.Count == 0
            ? new Dictionary<int, int>()
            : (await context.MentorAttendanceRecords
                .AsNoTracking()
                .Where(r => r.CourseSessionId.HasValue
                    && courseSessionIds.Contains(r.CourseSessionId.Value)
                    && (r.UserId == mentor.Id || r.Role == "Mentor"))
                .ToListAsync(cancellationToken))
                .GroupBy(r => r.CourseSessionId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalDurationSeconds));

        var roomIds = scopedCourseSessions.Select(x => x.Session.Availability?.RoomId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var mentorRoomAttendanceSeconds = roomIds.Count == 0
            ? new Dictionary<Guid, int>()
            : (await context.MentorAttendanceRecords
                .AsNoTracking()
                .Where(r => roomIds.Contains(r.RoomId) && (r.UserId == mentor.Id || r.Role == "Mentor"))
                .ToListAsync(cancellationToken))
                .GroupBy(r => r.RoomId)
                .ToDictionary(g => g.Key, g => g.Max(r => r.TotalDurationSeconds));

        // Homework (HomeworkAssignments + StudyCourseHomework)
        var bookingHomeworks = await context.HomeworkAssignments
            .AsNoTracking()
            .Include(h => h.Submission)
                .ThenInclude(s => s!.GradingResult)
            .Where(h => h.MentorId == mentor.Id && !h.IsDeleted)
            .ToListAsync(cancellationToken);

        var scopedBookingHomeworks = bookingHomeworks
            .Where(h =>
            {
                var anchor = ResolveBookingHomeworkAnchorUtc(h);
                return anchor >= fromStartUtc && anchor < toEndExclusiveUtc;
            })
            .ToList();

        var scopedCourseHomeworks = mentorCourses
            .SelectMany(c => c.Homework.Select(h => new { Course = c, Homework = h }))
            .Where(x =>
            {
                var anchor = ResolveCourseHomeworkAnchorUtc(x.Course, x.Homework);
                return anchor >= fromStartUtc && anchor < toEndExclusiveUtc && MentorAt(x.Course, anchor) == mentor.Id;
            })
            .ToList();

        // Reviews & Disputes — đọc đầy đủ CohortMentorReviews & CohortComplaints (mô hình lớp nhóm hiện hành) + dữ liệu lịch sử nếu có
        var legacyReviews = await context.MentorReviews
            .AsNoTracking()
            .Where(r => r.MentorId == mentor.Id
                && r.ModerationStatus == MentorReviewStatus.Approved
                && r.CreatedAtUtc >= fromStartUtc
                && r.CreatedAtUtc < toEndExclusiveUtc)
            .Select(r => new { r.Rating, r.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var cohortReviews = await context.CohortMentorReviews
            .AsNoTracking()
            .Where(r => r.MentorId == mentor.Id
                && !r.IsHidden
                && r.CreatedAtUtc >= fromStartUtc
                && r.CreatedAtUtc < toEndExclusiveUtc)
            .Select(r => new { r.Rating, r.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var allReviews = legacyReviews.Concat(cohortReviews).ToList();

        var legacyDisputes = await context.MentorDisputes
            .AsNoTracking()
            .Where(d => d.MentorId == mentor.Id
                && d.CreatedAtUtc >= fromStartUtc
                && d.CreatedAtUtc < toEndExclusiveUtc)
            .Select(d => new
            {
                d.CreatedAtUtc,
                IsOpen = d.Status == MentorDisputeStatus.Submitted
                    || d.Status == MentorDisputeStatus.UnderReview
                    || d.Status == MentorDisputeStatus.AwaitingResponse,
                IsRefund = d.CreditRefunded || d.IsRefundApproved == true,
            })
            .ToListAsync(cancellationToken);

        var cohortComplaints = await context.CohortComplaints
            .AsNoTracking()
            .Where(c => c.MentorId == mentor.Id
                && c.Category != CohortComplaint.CategoryPaymentEnrollment
                && c.CreatedAtUtc >= fromStartUtc
                && c.CreatedAtUtc < toEndExclusiveUtc)
            .Select(c => new
            {
                c.CreatedAtUtc,
                IsOpen = c.Status == CohortComplaint.Open || c.Status == CohortComplaint.UnderReview,
                IsRefund = c.RefundReviewRequested && c.Status == CohortComplaint.Resolved,
            })
            .ToListAsync(cancellationToken);

        var allDisputes = legacyDisputes.Concat(cohortComplaints).ToList();

        // Aggregate session events for overall + weekly + monthly
        var weeklyBuckets = new Dictionary<DateOnly, BucketAccumulator>();
        var monthlyBuckets = new Dictionary<DateOnly, BucketAccumulator>();
        var recentSessions = new List<MentorKpiRecentSessionItemResponse>();

        int completedValid = 0;
        int confirmedMinutes = 0;
        int scheduledCompletedMinutes = 0;
        int cancelled = 0;
        int cancelledByMentor = 0;
        int cancelledByLearner = 0;
        int missedOrNoShow = 0;
        int upcomingOrIncomplete = 0;

        // Deduplicate by logical class session (CohortId+Number or CourseId+Number) so makeup/rescheduled sessions never double-count.
        var courseSessionGroups = scopedCourseSessions
            .GroupBy(x => x.Course.CohortId.HasValue
                ? $"cohort:{x.Course.CohortId.Value}:{x.Session.Number}"
                : $"course:{x.Course.Id}:{x.Session.Number}")
            .ToList();

        foreach (var group in courseSessionGroups)
        {
            var items = group.ToList();
            var rep = items[0];
            var course = rep.Course;
            var s = rep.Session;
            var startUtc = s.ScheduledAt;
            var scheduledMins = course.SessionMinutes > 0 ? course.SessionMinutes : 60;
            var endUtc = s.Availability?.EndTime ?? startUtc.AddMinutes(scheduledMins);
            var roomId = s.Availability?.RoomId;
            var observedBySession = items.Max(x => courseAttendanceSeconds.GetValueOrDefault(x.Session.Id, 0));
            var observedByRoom = roomId.HasValue ? mentorRoomAttendanceSeconds.GetValueOrDefault(roomId.Value, 0) : 0;
            var observedSecs = Math.Max(observedBySession, observedByRoom);
            var observedMins = (int)Math.Round(observedSecs / 60.0);

            var hasConfirmedPresent = items.Any(x => x.Session.Status == "Present");
            var hasConfirmedAttendance = items.Any(x => x.Session.Status is "Present" or "Absent" or "ExcusedAbsent");
            // Chỉ công nhận buổi hoàn thành khi có điểm danh chính thức (không tự đánh dấu hoàn thành chỉ vì đã qua giờ Scheduled).
            var mentorAttendedRoom = observedMins > 0;
            var isPresent = hasConfirmedPresent || (mentorAttendedRoom && endUtc <= nowUtc && hasConfirmedAttendance && items.All(x => x.Session.Status is "Present" or "Absent" or "ExcusedAbsent"));
            var isCancelled = !isPresent && items.All(x => x.Session.Status is "CancelledByMentor" or "CancelledByLearner" or "CancelledByAdmin" or "Cancelled");
            var isMissed = !isPresent && !isCancelled && !mentorAttendedRoom && items.Any(x => x.Session.Status is "Absent" or "ExcusedAbsent");
            var isUpcoming = !isPresent && !isCancelled && !isMissed;

            var sessionConfirmedMins = isPresent
                ? (observedMins > 0 ? observedMins : scheduledMins)
                : observedMins;

            if (isPresent)
            {
                completedValid++;
                scheduledCompletedMinutes += scheduledMins;
                confirmedMinutes += sessionConfirmedMins;
            }
            else if (isCancelled)
            {
                cancelled++;
                if (items.Any(x => x.Session.Status == "CancelledByMentor"))
                    cancelledByMentor++;
                else if (items.Any(x => x.Session.Status == "CancelledByLearner"))
                    cancelledByLearner++;
            }
            else if (isMissed)
            {
                missedOrNoShow++;
            }
            else if (isUpcoming)
            {
                upcomingOrIncomplete++;
            }

            var w = GetBucket(weeklyBuckets, GetWeekStartLocal(startUtc));
            var m = GetBucket(monthlyBuckets, GetMonthStartLocal(startUtc));
            foreach (var bucket in new[] { w, m })
            {
                bucket.TotalAssignedSessions++;
                if (isPresent)
                {
                    bucket.CompletedValidSessions++;
                    bucket.ScheduledCompletedMinutes += scheduledMins;
                    bucket.ConfirmedTeachingMinutes += sessionConfirmedMins;
                }
                else if (isCancelled)
                {
                    bucket.CancelledSessionsCount++;
                }
                else if (isMissed)
                {
                    bucket.MissedOrNoShowSessionsCount++;
                }
                else if (isUpcoming)
                {
                    bucket.UpcomingOrIncompleteSessionsCount++;
                }
            }

            var isCohortClass = course.CohortId.HasValue || items.Count > 1;
            recentSessions.Add(new MentorKpiRecentSessionItemResponse
            {
                SourceType = isCohortClass ? "CohortGroup" : "StudyCourse",
                BookingOrSessionId = s.Id,
                RoomId = roomId,
                LearnerName = isCohortClass
                    ? $"{course.Cohort?.Name ?? course.Name} ({items.Count} học viên)"
                    : FormatUserName(course.Learner, course.Name),
                JlptLevel = course.CertificateLevel?.Code,
                Status = isPresent ? "Present" : s.Status,
                ScheduledStartUtc = startUtc,
                ScheduledEndUtc = endUtc,
                ScheduledMinutes = scheduledMins,
                ObservedMentorMinutes = observedMins,
                AttendedAtUtc = items.Select(x => x.Session.ConfirmedAt).FirstOrDefault(t => t.HasValue),
                IsValidCompleted = isPresent
            });
        }

        // Aggregate homework & turnaround
        var turnaroundSamples = new List<double>();
        int bookingHwSubmitted = 0;
        int bookingHwGraded = 0;

        foreach (var hw in scopedBookingHomeworks)
        {
            var anchor = ResolveBookingHomeworkAnchorUtc(hw);
            var sub = hw.Submission is { IsDeleted: false } validSub ? validSub : null;
            var grade = sub?.GradingResult is { IsDeleted: false } validGrade ? validGrade : null;
            DateTime? gradedAt = grade?.GradedAt ?? hw.PublishedFeedbackAtUtc;

            if (sub is not null) bookingHwSubmitted++;
            if (gradedAt.HasValue) bookingHwGraded++;

            double? turnaround = null;
            if (sub is not null && gradedAt.HasValue && gradedAt.Value >= sub.SubmittedAt)
            {
                turnaround = (gradedAt.Value - sub.SubmittedAt).TotalMinutes;
                turnaroundSamples.Add(turnaround.Value);
            }

            var w = GetBucket(weeklyBuckets, GetWeekStartLocal(anchor));
            var m = GetBucket(monthlyBuckets, GetMonthStartLocal(anchor));
            foreach (var bucket in new[] { w, m })
            {
                bucket.HomeworkAssignedCount++;
                if (sub is not null) bucket.HomeworkSubmittedCount++;
                if (gradedAt.HasValue) bucket.HomeworkGradedCount++;
                if (turnaround.HasValue) bucket.TurnaroundSamples.Add(turnaround.Value);
            }
        }

        int courseHwSubmitted = 0;
        int courseHwGraded = 0;
        foreach (var item in scopedCourseHomeworks)
        {
            var hw = item.Homework;
            var anchor = ResolveCourseHomeworkAnchorUtc(item.Course, hw);
            var isSubmitted = hw.SubmittedAt.HasValue;
            var isGraded = hw.Passed.HasValue && hw.ReviewedAt.HasValue;

            if (isSubmitted) courseHwSubmitted++;
            if (isGraded) courseHwGraded++;

            double? turnaround = null;
            if (hw.SubmittedAt.HasValue && hw.ReviewedAt.HasValue && hw.ReviewedAt.Value >= hw.SubmittedAt.Value)
            {
                turnaround = (hw.ReviewedAt.Value - hw.SubmittedAt.Value).TotalMinutes;
                turnaroundSamples.Add(turnaround.Value);
            }

            var w = GetBucket(weeklyBuckets, GetWeekStartLocal(anchor));
            var m = GetBucket(monthlyBuckets, GetMonthStartLocal(anchor));
            foreach (var bucket in new[] { w, m })
            {
                bucket.HomeworkAssignedCount++;
                if (isSubmitted) bucket.HomeworkSubmittedCount++;
                if (isGraded) bucket.HomeworkGradedCount++;
                if (turnaround.HasValue) bucket.TurnaroundSamples.Add(turnaround.Value);
            }
        }

        // Aggregate reviews & disputes
        var ratingDist = new Dictionary<int, int>
        {
            [1] = 0,
            [2] = 0,
            [3] = 0,
            [4] = 0,
            [5] = 0
        };

        foreach (var review in allReviews)
        {
            var star = Math.Clamp(review.Rating, 1, 5);
            ratingDist[star] = ratingDist.GetValueOrDefault(star, 0) + 1;

            var w = GetBucket(weeklyBuckets, GetWeekStartLocal(review.CreatedAtUtc));
            var m = GetBucket(monthlyBuckets, GetMonthStartLocal(review.CreatedAtUtc));
            w.ReviewRatings.Add(review.Rating);
            m.ReviewRatings.Add(review.Rating);
        }

        foreach (var dispute in allDisputes)
        {
            var w = GetBucket(weeklyBuckets, GetWeekStartLocal(dispute.CreatedAtUtc));
            var m = GetBucket(monthlyBuckets, GetMonthStartLocal(dispute.CreatedAtUtc));
            w.DisputesCount++;
            m.DisputesCount++;
        }

        var openDisputes = allDisputes.Count(d => d.IsOpen);
        var resolvedRefunds = allDisputes.Count(d => d.IsRefund);

        return new MentorKpiSummaryResponse
        {
            MentorId = mentor.Id,
            MentorName = FormatUserName(mentor, $"Mentor #{mentor.Id}"),
            FromDate = fromDate,
            ToDate = toDate,
            TeachingJlptLevels = qualifications,

            TotalAssignedSessions = courseSessionGroups.Count,
            AssignedBookingsCount = 0,
            AssignedCourseSessionsCount = courseSessionGroups.Count,

            CompletedValidSessions = completedValid,
            ConfirmedTeachingMinutes = confirmedMinutes,
            ScheduledCompletedMinutes = scheduledCompletedMinutes,

            CancelledSessionsCount = cancelled,
            CancelledByMentorCount = cancelledByMentor,
            CancelledByLearnerCount = cancelledByLearner,
            MissedOrNoShowSessionsCount = missedOrNoShow,
            UpcomingOrIncompleteSessionsCount = upcomingOrIncomplete,

            HomeworkAssignedCount = scopedBookingHomeworks.Count + scopedCourseHomeworks.Count,
            HomeworkSubmittedCount = bookingHwSubmitted + courseHwSubmitted,
            HomeworkGradedCount = bookingHwGraded + courseHwGraded,
            BookingHomeworkAssignedCount = scopedBookingHomeworks.Count,
            BookingHomeworkSubmittedCount = bookingHwSubmitted,
            BookingHomeworkGradedCount = bookingHwGraded,
            CourseHomeworkAssignedCount = scopedCourseHomeworks.Count,
            CourseHomeworkSubmittedCount = courseHwSubmitted,
            CourseHomeworkGradedCount = courseHwGraded,

            AverageFeedbackTurnaroundMinutes = turnaroundSamples.Count > 0
                ? Math.Round(turnaroundSamples.Average(), 1)
                : null,
            FeedbackTurnaroundSampleCount = turnaroundSamples.Count,

            AverageRating = allReviews.Count > 0 ? Math.Round(allReviews.Average(r => r.Rating), 2) : null,
            TotalReviewsCount = allReviews.Count,
            RatingDistribution = ratingDist,
            TotalDisputesCount = allDisputes.Count,
            OpenDisputesCount = openDisputes,
            ResolvedRefundCount = resolvedRefunds,

            WeeklyBreakdown = weeklyBuckets
                .OrderByDescending(kv => kv.Key)
                .Select(kv => ToPeriodResponse(
                    $"W{ISOWeek.GetWeekOfYear(kv.Key.ToDateTime(TimeOnly.MinValue)):D2} ({kv.Key:dd/MM})",
                    kv.Key,
                    kv.Key.AddDays(6),
                    kv.Value))
                .ToList(),

            MonthlyBreakdown = monthlyBuckets
                .OrderByDescending(kv => kv.Key)
                .Select(kv => ToPeriodResponse(
                    $"{kv.Key.Year:D4}-{kv.Key.Month:D2}",
                    kv.Key,
                    kv.Key.AddMonths(1).AddDays(-1),
                    kv.Value))
                .ToList(),

            RecentSessions = recentSessions
                .OrderByDescending(s => s.ScheduledStartUtc)
                .Take(30)
                .ToList(),

            MetricSources = new Dictionary<string, string>
            {
                ["AssignedAndCompletedSessions"] = "Tổng hợp từ lịch buổi học chính thức của lớp nhóm (loại trừ lớp đã hủy và buổi chuyển lớp). Buổi hợp lệ là buổi đã được xác nhận hoàn thành.",
                ["ConfirmedTeachingMinutes"] = "Ưu tiên tổng thời lượng hiện diện thực tế của Giáo viên; nếu chưa có dữ liệu thời lượng trực tiếp thì tính theo thời lượng kế hoạch (phút).",
                ["ScheduledCompletedMinutes"] = "Tổng thời lượng kế hoạch (phút) của các buổi đã hoàn thành hợp lệ.",
                ["HomeworkMetrics"] = "Tổng hợp từ bài tập trong lớp và bài tập về nhà đã giao, đã nộp và đã chấm.",
                ["AverageFeedbackTurnaroundMinutes"] = "Thời gian phản hồi trung bình (phút) tính từ lúc học viên nộp bài đến khi Giáo viên chấm xong.",
                ["QualityMetrics"] = "Tổng hợp từ đánh giá chất lượng giảng dạy của học viên và các phản hồi/khiếu nại về lớp học theo múi giờ Việt Nam.",
                ["CompensationScopeNotice"] = "Hệ thống không áp dụng trọng số KPI hay quy đổi thưởng, phạt hoặc thù lao."
            }
        };
    }

    private async Task<int> ResolveReportingPeriodDaysAsync(CancellationToken ct)
    {
        var days = await context.MentorGroupPolicies
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsArchivedRevision && !p.IsDeleted)
            .OrderByDescending(p => p.LastAppliedAtUtc)
            .ThenByDescending(p => p.Version)
            .Select(p => (int?)p.KpiReportingPeriodDays)
            .FirstOrDefaultAsync(ct);
        return days is > 0 ? days.Value : 30;
    }

    private static bool TryResolveWindow(
        DateOnly? from,
        DateOnly? to,
        int defaultReportingPeriodDays,
        out DateOnly fromDate,
        out DateOnly toDate,
        out string? error)
    {
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.Add(VietnamOffset));
        toDate = to ?? todayVn;
        var resolvedPeriod = Math.Max(1, defaultReportingPeriodDays);
        fromDate = from ?? toDate.AddDays(-(resolvedPeriod - 1));

        if (fromDate > toDate)
        {
            error = "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.";
            return false;
        }

        var maxRangeDays = Math.Max(370, resolvedPeriod);
        if (toDate.DayNumber - fromDate.DayNumber > maxRangeDays)
        {
            error = $"Khoảng thời gian thống kê tối đa là {maxRangeDays} ngày.";
            return false;
        }

        error = null;
        return true;
    }

    private static DateTime ResolveBookingHomeworkAnchorUtc(HomeworkAssignment hw)
        => hw.Submission?.GradingResult?.GradedAt
           ?? hw.Submission?.SubmittedAt
           ?? hw.CreatedDate
           ?? hw.DueAt
           ?? DateTime.UtcNow;

    private static DateTime ResolveCourseHomeworkAnchorUtc(StudyCourse course, StudyCourseHomework hw)
        => hw.ReviewedAt
           ?? hw.SubmittedAt
           ?? course.StartsAt
           ?? hw.DueAt;

    private static DateOnly GetWeekStartLocal(DateTime utc)
    {
        var localDate = DateOnly.FromDateTime(utc.Add(VietnamOffset));
        var offsetFromMonday = ((int)localDate.DayOfWeek + 6) % 7;
        return localDate.AddDays(-offsetFromMonday);
    }

    private static DateOnly GetMonthStartLocal(DateTime utc)
    {
        var localDate = DateOnly.FromDateTime(utc.Add(VietnamOffset));
        return new DateOnly(localDate.Year, localDate.Month, 1);
    }

    private static string FormatUserName(UserAccount? user, string fallback)
    {
        if (user is null) return fallback;
        var fullName = string.Join(" ", new[] { user.FirstName, user.LastName }
            .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        return string.IsNullOrWhiteSpace(fullName) ? (user.Email ?? fallback) : fullName;
    }

    private static BucketAccumulator GetBucket(Dictionary<DateOnly, BucketAccumulator> map, DateOnly key)
    {
        if (!map.TryGetValue(key, out var bucket))
        {
            bucket = new BucketAccumulator();
            map[key] = bucket;
        }

        return bucket;
    }

    private static MentorKpiPeriodItemResponse ToPeriodResponse(
        string periodKey,
        DateOnly periodStart,
        DateOnly periodEnd,
        BucketAccumulator b) => new()
        {
            PeriodKey = periodKey,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalAssignedSessions = b.TotalAssignedSessions,
            CompletedValidSessions = b.CompletedValidSessions,
            ConfirmedTeachingMinutes = b.ConfirmedTeachingMinutes,
            ScheduledCompletedMinutes = b.ScheduledCompletedMinutes,
            CancelledSessionsCount = b.CancelledSessionsCount,
            MissedOrNoShowSessionsCount = b.MissedOrNoShowSessionsCount,
            UpcomingOrIncompleteSessionsCount = b.UpcomingOrIncompleteSessionsCount,
            HomeworkAssignedCount = b.HomeworkAssignedCount,
            HomeworkSubmittedCount = b.HomeworkSubmittedCount,
            HomeworkGradedCount = b.HomeworkGradedCount,
            AverageFeedbackTurnaroundMinutes = b.TurnaroundSamples.Count > 0
                ? Math.Round(b.TurnaroundSamples.Average(), 1)
                : null,
            FeedbackTurnaroundSampleCount = b.TurnaroundSamples.Count,
            ReviewsCount = b.ReviewRatings.Count,
            AverageRating = b.ReviewRatings.Count > 0 ? Math.Round(b.ReviewRatings.Average(), 2) : null,
            DisputesCount = b.DisputesCount
        };

    private sealed class BucketAccumulator
    {
        public int TotalAssignedSessions { get; set; }
        public int CompletedValidSessions { get; set; }
        public int ConfirmedTeachingMinutes { get; set; }
        public int ScheduledCompletedMinutes { get; set; }
        public int CancelledSessionsCount { get; set; }
        public int MissedOrNoShowSessionsCount { get; set; }
        public int UpcomingOrIncompleteSessionsCount { get; set; }
        public int HomeworkAssignedCount { get; set; }
        public int HomeworkSubmittedCount { get; set; }
        public int HomeworkGradedCount { get; set; }
        public List<double> TurnaroundSamples { get; } = [];
        public List<int> ReviewRatings { get; } = [];
        public int DisputesCount { get; set; }
    }
}
