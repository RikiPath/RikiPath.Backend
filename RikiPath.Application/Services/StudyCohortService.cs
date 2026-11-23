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
    // Ghi danh đã chuyển lớp ("Transferred") giải phóng chỗ ở lớp nguồn đúng một lần, giữ nguyên lịch sử.
    private static bool ReservesSeat(StudyCourse c, DateTime now) => (c.PaymentStatus == "Paid" && c.Status != "PaymentReview" && c.Status != "Cancelled" && c.Status != "Transferred")
        || (c.PaymentStatus == "Pending" && c.PaymentExpiresAt > now);

    // Trạng thái thanh toán hiển thị dùng chung cho chi tiết khóa học và danh sách lớp.
    internal static string PaymentStateOf(StudyCourse c, DateTime now) => c.PaymentStatus switch
    {
        "Pending" => c.PaymentExpiresAt > now ? "AwaitingPayment" : "Expired",
        "Expired" => "Expired",
        "Failed" => "Failed",
        "Paid" => c.Status == "PaymentReview" ? "PaidUnderReview" : "Confirmed",
        "Cancelled" => "Cancelled",
        _ => c.PaymentStatus
    };

    private static DateTime EnrollmentCutoffUtc(StudyCohort cohort)
    {
        // Buổi đầu đã duyệt là mốc khai giảng; policy của slot là snapshot bất biến.
        var first = cohort.Sessions.Where(s => s.Availability.IsApproved && !s.Availability.IsDeleted
                && s.Availability.RejectedAt == null)
            .OrderBy(s => s.Availability.StartTime).FirstOrDefault();
        return first is null ? cohort.StartsAt : first.Availability.StartTime.AddMinutes(
            -(first.Availability.GroupPolicy?.EnrollmentCutoffMinutesBeforeFirstSession ?? 0));
    }

    private async Task<bool> HasLearnerConflictAsync(int learnerId, IReadOnlyList<MentorAvailability> proposed, int excludeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var courses = await uow.StudyCourses.Query().Where(e => e.Id != excludeId && e.LearnerId == learnerId
            && e.Status != "Cancelled"
            && (e.PaymentStatus == "Paid" || (e.PaymentStatus == "Pending" && e.PaymentExpiresAt > now)))
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
            Require(string.IsNullOrWhiteSpace(filter.Intensity) || SessionsPerWeekOfIntensity(filter.Intensity).HasValue,
                "Cường độ phải là Light, Standard hoặc Advanced.");
            Require(!filter.FromTime.HasValue || !filter.ToTime.HasValue || filter.FromTime <= filter.ToTime,
                "Giờ bắt đầu lọc phải trước hoặc bằng giờ kết thúc.");
        }
        var mentorFilter = filter?.MentorId;
        var query = uow.StudyCohorts.Query().Include(x => x.Plan).ThenInclude(x => x.CertificateLevel)
            .Include(x => x.Mentor).Include(x => x.Sessions).ThenInclude(x => x.Availability).ThenInclude(x => x.GroupPolicy)
            .Include(x => x.Enrollments).AsSplitQuery()
            .Where(x => Actor.Role == Role.Admin || (Actor.Role == Role.Mentor && x.MentorId == Actor.Id)
                || (x.Status == "Published" && x.StartsAt > now && x.Plan.IsPublished));
        if (mentorFilter.HasValue) query = query.Where(x => x.MentorId == mentorFilter.Value);
        var rows = await query.OrderBy(x => x.StartsAt).ToListAsync(ct);
        if (filter is not null) rows = rows.Where(x => MatchesDiscoveryFilter(x, filter)).ToList();
        var policy = await EffectiveGroupPolicyAsync(now, ct);
        return rows.Select(x =>
        {
            var enrolledCount = x.Enrollments.Count(c => ReservesSeat(c, now));
            var seatsAvailable = Math.Max(0, x.Capacity - enrolledCount);
            var requiresEntitlement = x.Sessions.Any(s => s.Availability.GroupPolicyId.HasValue);
            var decision = !requiresEntitlement
                ? new GroupEnrollmentEntitlementDecision(true, RequiresCheckout: true)
                : (groupEntitlementContract ?? new GroupEnrollmentEntitlementContract())
                    .EvaluateGroupEnrollment(x, x.Plan, Actor.Id, null);
            var myEnrollment = Actor.Role == Role.Learner
                ? x.Enrollments.FirstOrDefault(c => c.LearnerId == Actor.Id && ReservesSeat(c, now))
                : null;
            var withinScheduleWindow = x.Status == "Published" && EnrollmentCutoffUtc(x) > now
                && (!x.Sessions.Any() || x.Sessions.Min(s => s.Availability.StartTime) > now);
            var hasApprovedEntitlement = requiresEntitlement && decision.IsAuthorized && !decision.RequiresCheckout;
            return new
            {
                x.Id,
                x.PlanId,
                x.Name,
                x.MentorId,
                MentorName = $"{x.Mentor.FirstName} {x.Mentor.LastName}".Trim(),
                x.StartsAt,
                x.EndsAt,
                x.Capacity,
                x.CapacitySnapshot,
                x.PolicyVersionSnapshot,
                x.SourceProposalId,
                x.Status,
                x.Revision,
                Level = x.Plan.CertificateLevel.Code,
                x.Plan.TotalSessions,
                x.Plan.SessionMinutes,
                x.Plan.SessionsPerWeek,
                Intensity = IntensityOf(x.Plan.SessionsPerWeek),
                IntensityLabel = IntensityLabelOf(x.Plan.SessionsPerWeek),
                IsRecommendedIntensity = x.Plan.SessionsPerWeek == policy.DefaultSessionsPerWeek,
                TotalTeachingHours = TotalTeachingHoursOf(x.Plan.TotalSessions, x.Plan.SessionMinutes),
                SchedulePattern = SchedulePatternOf(x),
                EnrollmentClosesAtUtc = EnrollmentCutoffUtc(x),
                EnrolledCount = enrolledCount,
                RequiresEntitlement = requiresEntitlement,
                HasApprovedEntitlement = hasApprovedEntitlement,
                EntitlementReference = decision.IsAuthorized ? decision.EntitlementReference : null,
                // Thanh toán PayOS là đường ghi danh chuẩn; thiếu quyền Admin cấp không còn chặn ghi danh.
                EntitlementStatusMessage = (string?)null,
                CheckoutMode = hasApprovedEntitlement ? "AdminEntitlement" : "PayOS",
                MyEnrollmentCourseId = myEnrollment?.Id,
                MyEnrollmentStatus = myEnrollment?.Status,
                MyEnrollmentPaymentState = myEnrollment is null ? null : PaymentStateOf(myEnrollment, now),
                EnrollmentOpen = withinScheduleWindow && seatsAvailable > 0 && myEnrollment is null,
                x.Plan.Price,
                SeatsAvailable = seatsAvailable,
                Sessions = x.Sessions.OrderBy(s => s.Number).Select(s => new
                {
                    s.Number,
                    s.AvailabilityId,
                    s.Availability.RoomId,
                    s.Availability.StartTime,
                    s.Availability.EndTime
                })
            };
        }).ToList();
    });

    public Task<ApiResponse<object>> SaveCohortAsync(int? id, SaveStudyCohort r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Require(!string.IsNullOrWhiteSpace(r.Name) && r.Name.Length <= 200 && r.Capacity > 0,
            "Nhập tên lớp và sĩ số hợp lệ.");
        Require(r.AvailabilityIds is { Count: > 0 and <= 500 } && r.AvailabilityIds.Distinct().Count() == r.AvailabilityIds.Count,
            "Chọn lịch cố định, không trùng buổi.");
        int savedId = 0;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            // Serialize every timetable mutation against 1–1 bookings as well.
            await uow.StudyCourses.LockScheduleAsync(r.MentorId, r.MentorId, token);
            if (id.HasValue) await uow.StudyCourses.LockCohortAsync(id.Value, token);
            var p = await uow.StudyCoursePlans.GetByIdAsync(r.PlanId, token);
            Require(p is { SessionType: "Group", SessionMinutes: not null, TotalSessions: not null }, "Chọn gói học nhóm đã cấu hình đủ buổi.");
            var mentorAccount = await uow.UserAccounts.Query()
                .Include(x => x.MentorProfile)
                .FirstOrDefaultAsync(x => x.Id == r.MentorId && x.Role == Role.Mentor && !x.IsDeleted, token);
            Require(mentorAccount is not null, "Giáo viên không hợp lệ hoặc đã ngưng hoạt động.");
            Rule(mentorAccount!.MentorProfile?.VerificationStatus == MentorVerificationStatus.Approved,
                "MENTOR_NOT_VERIFIED",
                "Chỉ được giao lớp cho Giáo viên đã được phê duyệt hồ sơ.", HttpStatusCode.Conflict);
            if (uow.MentorTeachingQualifications is not null)
            {
                var hasQualification = await uow.MentorTeachingQualifications.Query()
                    .AnyAsync(q => q.MentorUserId == r.MentorId && q.CertificateLevelId == p!.CertificateLevelId && q.IsActive, token);
                var hasApprovedProposal = await uow.MentorTeachingProposals.Query()
                    .AnyAsync(pr => pr.MentorId == r.MentorId
                        && pr.Status == MentorTeachingProposalStatus.Approved
                        && pr.CertificateLevelId == p!.CertificateLevelId
                        && (!r.SourceProposalId.HasValue || pr.Id == r.SourceProposalId.Value), token);
                Rule(hasQualification || hasApprovedProposal, "MENTOR_NOT_QUALIFIED_FOR_LEVEL",
                    "Giáo viên chưa có đề xuất mở lớp đã duyệt hoặc phân quyền giảng dạy cho cấp độ của gói học này.",
                    HttpStatusCode.Conflict);
            }
            var c = id.HasValue ? await uow.StudyCohorts.Query().Include(x => x.Sessions).Include(x => x.Enrollments)
                .SingleOrDefaultAsync(x => x.Id == id.Value, token) : new StudyCohort();
            Require(c is not null, "Lớp vừa thay đổi hoặc không tồn tại.");
            Rule(!id.HasValue || c!.Revision == r.Revision, "COHORT_SCHEDULE_REVISION_CONFLICT",
                "Lớp vừa được cập nhật bởi thao tác khác. Vui lòng tải lại trước khi lưu.", HttpStatusCode.Conflict);
            Require(!id.HasValue || c!.MentorId == r.MentorId, "Không đổi Giáo viên trực tiếp; hãy dùng luồng thay thế Giáo viên.");
            Rule(!id.HasValue || !c!.Enrollments.Any(x => ReservesSeat(x, DateTime.UtcNow) || x.Status == "PaymentReview"),
                "COHORT_SCHEDULE_ALREADY_PUBLISHED",
                "Lớp đang có học viên, giữ chỗ hoặc đơn đang kiểm tra thanh toán. Xử lý qua yêu cầu điều chỉnh, không sửa trực tiếp.",
                HttpStatusCode.Conflict);
            var slots = await uow.MentorAvailabilities.Query().Include(x => x.GroupPolicy).Where(x => r.AvailabilityIds.Contains(x.Id)
                && x.MentorId == r.MentorId && !x.IsDeleted && x.IsApproved && x.RejectedAt == null
                && x.StartTime > DateTime.UtcNow && x.ChangeRequestedAt == null).OrderBy(x => x.StartTime).ToListAsync(token);
            Require(slots.Count == r.AvailabilityIds.Count, "Chọn đủ tổng buổi của gói từ lịch rảnh đã duyệt.");
            Require(slots.All(x => x.GroupPolicy is not null && x.GroupPolicyId.HasValue
                && x.SessionMinutesSnapshot == p!.SessionMinutes
                && (x.EndTime - x.StartTime).TotalMinutes == p.SessionMinutes
                && x.MaxLearners == x.GroupPolicy.MaxLearners
                && x.BookedCount == 0),
                "Lịch lớp phải theo chính sách Admin, cùng thời lượng gói và chưa có người đặt.");
            // Lớp nhóm: 1 Mentor chính, 1..sức chứa chính sách học viên (không chờ đủ sĩ số để mở lớp).
            var cohortPolicy = slots[0].GroupPolicy!;
            Rule(r.Capacity >= 1 && slots.All(x => r.Capacity <= x.GroupPolicy!.MaxLearners), "COURSE_PLAN_INVALID_CAPACITY",
                $"Sĩ số lớp phải từ 1 đến {cohortPolicy.MaxLearners} học viên theo chính sách.");
            var standardPlan = IsPolicyStandardPlan(p!, cohortPolicy);
            if (r.Publish && standardPlan)
                EnsureWholeCourseSchedule(slots, cohortPolicy.CourseDurationWeeks, p!.SessionsPerWeek, p.SessionMinutes!.Value);
            Require(slots.Count == p!.TotalSessions, "Chọn đủ tổng buổi của gói từ lịch rảnh đã duyệt.");
            Require(!await uow.StudyCohorts.Query().AnyAsync(x => x.Id != (id ?? 0)
                && x.Sessions.Any(s => r.AvailabilityIds.Contains(s.AvailabilityId)), token), "Lịch đã thuộc lớp khác.");
            // Trùng lịch dạy của Mentor (lớp đã công bố khác, lịch 1-1 đã xác nhận, buổi khóa 1-1) qua dịch vụ chuẩn,
            // chạy sau LockScheduleAsync(mentor) ở trên nên hai lần công bố đồng thời được tuần tự hóa.
            await EnsureNoTeachingConflictAsync(r.MentorId,
                slots.Select((s, i) => new ProposedTeachingInterval(s.StartTime, s.EndTime, i + 1)).ToList(),
                new TeachingConflictOptions { ExcludeCohortId = id, ExcludeAvailabilityIds = r.AvailabilityIds }, token);
            Require(!await uow.StudyCourses.Query().AnyAsync(x => x.PaymentStatus == "Paid"
                && x.Sessions.Any(s => s.AvailabilityId.HasValue && r.AvailabilityIds.Contains(s.AvailabilityId.Value)), token), "Lịch đã có học viên 1–1.");
            for (var i = 1; i < slots.Count; i++) Require(slots[i - 1].EndTime <= slots[i].StartTime, "Các buổi học bị trùng giờ.");
            // Gói chuẩn được kiểm tra theo số tuần của chính sách; gói cũ giữ kiểm tra theo số tháng.
            if (!(r.Publish && standardPlan))
                Require(slots[^1].EndTime <= slots[0].StartTime.AddMonths(p!.DurationMonths), "Lịch lớp vượt thời gian học của gói.");
            Require(!p.TargetExamDate.HasValue || slots[^1].EndTime <= p.TargetExamDate, "Lớp phải kết thúc trước kỳ thi mục tiêu.");
            var weekly = slots.GroupBy(s => { var d = LocalTime(s.StartTime).Date; return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); });
            Require(weekly.All(g => g.Count() <= p.SessionsPerWeek), "Số buổi trong tuần vượt cấu hình gói.");
            c!.Name = r.Name.Trim(); c.PlanId = r.PlanId; c.MentorId = r.MentorId; c.Capacity = r.Capacity;
            c.CapacitySnapshot = r.Capacity;
            c.GroupPolicyId = slots[0].GroupPolicyId;
            c.PolicyVersionSnapshot = slots[0].GroupPolicy?.Version ?? slots[0].PolicyVersion;
            if (!id.HasValue && r.SourceProposalId.HasValue)
            {
                var srcProposal = await uow.MentorTeachingProposals.Query().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == r.SourceProposalId.Value, token);
                Require(srcProposal is not null && srcProposal.Status == MentorTeachingProposalStatus.Approved
                    && srcProposal.MentorId == r.MentorId,
                    "Đề xuất nguồn không hợp lệ hoặc chưa được duyệt cho Giáo viên này.");
                c.SourceProposalId = srcProposal!.Id;
            }
            c.StartsAt = slots[0].StartTime; c.EndsAt = slots[^1].EndTime;
            c.Status = r.Publish ? "Published" : "Draft";
            if (r.Publish) Require(p.IsPublished, "Gói cần được mở bán trước khi công bố lớp.");
            var old = c.Sessions.ToDictionary(x => x.AvailabilityId);
            c.Sessions.RemoveAll(x => !r.AvailabilityIds.Contains(x.AvailabilityId));
            for (var i = 0; i < slots.Count; i++)
            {
                if (old.TryGetValue(slots[i].Id, out var existing)) existing.Number = i + 1;
                else c.Sessions.Add(new() { Number = i + 1, AvailabilityId = slots[i].Id });
            }
            if (!id.HasValue) await uow.StudyCohorts.AddAsync(c, token); else c.Revision++;
            await uow.SaveChangesAsync(token); savedId = c.Id;
            await uow.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = Actor.Id,
                EntityType = "StudyCohort",
                EntityId = c.Id,
                Action = id.HasValue ? "Updated" : "Created",
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { c.Id, c.Name, c.PlanId, c.MentorId, c.Capacity, c.Status, c.Revision }),
                CreatedAtUtc = DateTime.UtcNow,
            }, token);
            await uow.SaveChangesAsync(token);
        }, ct);
        return new { Id = savedId };
    });

    private static readonly Lazy<TimeZoneInfo> CohortBusinessZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById(RikiPath.Domain.MentorScheduling.MentorSlotPolicy.BusinessTimeZoneId));

    private static DateTime LocalTime(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(MentorTeachingConflictService.Utc(utc), CohortBusinessZone.Value);

    private static DateOnly LocalWeekStartOf(DateTime utc)
    {
        var d = DateOnly.FromDateTime(LocalTime(utc));
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    }

    // Lịch toàn khóa (gói chuẩn): đúng {weeks} tuần liên tiếp (giờ Việt Nam, tuần bắt đầu thứ 2), mỗi tuần đúng {perWeek} buổi,
    // mỗi buổi đúng {minutes} phút. Báo rõ tuần sai đầu tiên.
    private static void EnsureWholeCourseSchedule(IReadOnlyList<MentorAvailability> slots, int weeks, int perWeek, int minutes)
    {
        const string code = "COHORT_SCHEDULE_INCOMPLETE";
        var wrongLength = slots.Select((s, i) => (Slot: s, Number: i + 1))
            .FirstOrDefault(x => (x.Slot.EndTime - x.Slot.StartTime).TotalMinutes != minutes);
        Rule(wrongLength.Slot is null, code,
            $"Buổi {wrongLength.Number} không đúng thời lượng {minutes} phút của gói.");
        var firstWeek = LocalWeekStartOf(slots[0].StartTime);
        var byWeek = slots.GroupBy(s => (LocalWeekStartOf(s.StartTime).DayNumber - firstWeek.DayNumber) / 7)
            .ToDictionary(g => g.Key, g => g.Count());
        string Label(int index) => firstWeek.AddDays(7 * index).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        for (var week = 0; week < weeks; week++)
        {
            var count = byWeek.GetValueOrDefault(week);
            Rule(count == perWeek, code,
                $"Tuần {week + 1} (bắt đầu thứ 2 {Label(week)}) có {count} buổi; khóa học cần đúng {perWeek} buổi mỗi tuần trong {weeks} tuần liên tiếp.");
        }
        var beyond = byWeek.Keys.Where(k => k >= weeks).OrderBy(k => k).Cast<int?>().FirstOrDefault();
        Rule(beyond is null, code,
            $"Tuần {beyond + 1} (bắt đầu thứ 2 {Label(beyond ?? 0)}) nằm ngoài {weeks} tuần của khóa học; khóa học cần đúng {perWeek} buổi mỗi tuần trong {weeks} tuần liên tiếp.");
    }

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
        if (!string.IsNullOrWhiteSpace(f.Intensity) && x.Plan.SessionsPerWeek != SessionsPerWeekOfIntensity(f.Intensity)) return false;
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
            && r.AvailabilityIds.Distinct().Count() == r.AvailabilityIds.Count, "Chọn Mentor và các khung giờ không trùng nhau.");
        var slots = await uow.MentorAvailabilities.Query().AsNoTracking()
            .Where(x => r!.AvailabilityIds.Contains(x.Id) && x.MentorId == r.MentorId && !x.IsDeleted && x.RejectedAt == null)
            .OrderBy(x => x.StartTime).Select(x => new { x.Id, x.StartTime, x.EndTime }).ToListAsync(ct);
        Require(slots.Count == r!.AvailabilityIds.Count, "Có khung giờ không tồn tại hoặc không thuộc Mentor đã chọn.");
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
