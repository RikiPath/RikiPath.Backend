using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class StudyCourseService(IUnitOfWork uow, IClaimService claims, IPaymentGatewayClient payments,
    IFileStorageService storage, AppSettings settings, IJlptExamSource? examSource = null,
    IGroupEnrollmentEntitlementContract? groupEntitlementContract = null,
    IMentorGroupPolicyService? groupPolicies = null,
    IMentorTeachingConflictService? teachingConflicts = null,
    IMentorLearnerSkillProfileService? skillProfiles = null) : IStudyCourseService
{
    private ClaimDTO Actor => claims.GetUserClaim();
    private sealed class CourseRuleException(string message, HttpStatusCode status = HttpStatusCode.BadRequest, string? code = null) : Exception(message)
    {
        public HttpStatusCode Status { get; } = status;
        public string? Code { get; } = code;
    }
    // Quy tắc nghiệp vụ có mã lỗi miền ổn định (dùng cho chuyển lớp/thay Mentor).
    private static void Rule(bool condition, string code, string message, HttpStatusCode status = HttpStatusCode.UnprocessableEntity)
    {
        if (!condition) throw new CourseRuleException(message, status, code);
    }
    private static void Require(bool condition, string message, HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        if (!condition) throw new CourseRuleException(message, status);
    }
    private static void RequireConflict(bool condition, string message) => Require(condition, message, HttpStatusCode.Conflict);
    private void Admin() { if (Actor.Role != Role.Admin) throw new UnauthorizedAccessException(); }
    private void Learner(StudyCourse c) { if (Actor.Role != Role.Learner || c.LearnerId != Actor.Id) throw new UnauthorizedAccessException(); }
    private void Teacher(StudyCourse c) { if (Actor.Role != Role.Mentor || (c.MentorId ?? c.Cohort?.MentorId) != Actor.Id) throw new UnauthorizedAccessException(); }
    private void CanRead(StudyCourse c)
    {
        if (Actor.Role != Role.Admin && !(Actor.Role == Role.Learner && c.LearnerId == Actor.Id)
            && !(Actor.Role == Role.Mentor && (c.MentorId ?? c.Cohort?.MentorId) == Actor.Id)) throw new UnauthorizedAccessException();
    }
    private IQueryable<StudyCourse> Details => uow.StudyCourses.Query().Include(x => x.CertificateLevel)
        .Include(x => x.Learner).Include(x => x.Mentor).Include(x => x.Cohort).Include(x => x.GroupPolicy)
        .Include(x => x.Sessions).Include(x => x.Homework).Include(x => x.RetakeRequests).Include(x => x.Audit).AsSplitQuery();
    private void Audit(StudyCourse c, string action, string detail)
        => c.Audit.Add(new() { ActorId = Actor.Id, Action = action, Detail = detail });
    private static void Active(StudyCourse c) => Require(c.Status == "Active" && c.PaymentStatus == "Paid", "Khóa chưa hoạt động hoặc đã chốt dữ liệu.");

    private async Task<ApiResponse<object>> Run(Func<Task<object>> action)
    {
        try { return ApiResponse<object>.Success(await action()); }
        catch (UnauthorizedAccessException) { return ApiResponse<object>.Fail("Bạn không có quyền thực hiện thao tác này.", HttpStatusCode.Forbidden); }
        catch (CourseRuleException ex) { return ex.Code is null ? ApiResponse<object>.Fail(ex.Message, ex.Status) : ApiResponse<object>.Fail(ex.Message, ex.Status, ex.Code); }
        catch (DbUpdateConcurrencyException) { return ApiResponse<object>.Fail("Dữ liệu vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict); }
        catch (DbUpdateException ex) when (SqlStateOf(ex) == "23505")
        { return ApiResponse<object>.Fail("Yêu cầu đã được xử lý hoặc dữ liệu bị trùng. Vui lòng tải lại.", HttpStatusCode.Conflict, "DUPLICATE_REQUEST"); }
        catch (DbUpdateException ex) when (SqlStateOf(ex) == "23P01")
        { return ApiResponse<object>.Fail("Lịch dạy bị trùng với lịch đã xác nhận. Vui lòng chọn khung giờ khác.", HttpStatusCode.Conflict, "MENTOR_SCHEDULE_CONFLICT"); }
        catch (DbUpdateException) { return ApiResponse<object>.Fail("Yêu cầu ghi danh bị xung đột dữ liệu hoặc vượt sức chứa.", HttpStatusCode.Conflict); }
    }
    private static string? SqlStateOf(Exception ex)
    {
        for (var e = ex.InnerException; e is not null; e = e.InnerException)
            if (e is System.Data.Common.DbException db && !string.IsNullOrEmpty(db.SqlState)) return db.SqlState;
        return null;
    }
    // Kiểm tra trùng lịch dạy chuẩn duy nhất; ném lỗi miền 409 với thông điệp đầu tiên.
    private IMentorTeachingConflictService TeachingConflicts => teachingConflicts ??= new MentorTeachingConflictService(uow, groupPolicies);
    private async Task EnsureNoTeachingConflictAsync(int mentorId, IReadOnlyList<ProposedTeachingInterval> proposed,
        TeachingConflictOptions? options, CancellationToken ct)
    {
        var report = await TeachingConflicts.CheckAsync(mentorId, proposed, options, ct);
        if (!report.IsValid) throw new CourseRuleException(report.First!.Message, HttpStatusCode.Conflict, report.First.Code);
    }
    private Task<ApiResponse<object>> Change(int id, Func<StudyCourse, Task<object>> change, CancellationToken ct) => Run(async () =>
    {
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var locked = await uow.StudyCourses.LockAsync(id, token);
            Require(locked is not null, "Không tìm thấy khóa học.");
            var course = await Details.SingleAsync(x => x.Id == id, token);
            CanRead(course);
            result = await change(course);
            await uow.SaveChangesAsync(token);
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> CatalogAsync(CancellationToken ct) => Run(async () =>
        await uow.CertificateLevels.Query().Include(x => x.CertificateType)
            .Where(x => !x.IsDeleted && !x.CertificateType.IsDeleted && x.CertificateType.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.Code, x.Description, CertificateType = x.CertificateType.Name }).ToListAsync(ct));

    public Task<ApiResponse<object>> PlansAsync(CancellationToken ct) => Run(async () =>
    {
        var policy = await EffectiveGroupPolicyAsync(DateTime.UtcNow, ct);
        var rows = await uow.StudyCoursePlans.Query().Include(x => x.CertificateLevel)
            .Where(x => Actor.Role == Role.Admin || (x.IsPublished && !x.CertificateLevel.IsDeleted && !x.CertificateLevel.CertificateType.IsDeleted && x.CertificateLevel.CertificateType.IsActive))
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.CertificateLevelId, Level = x.CertificateLevel.Code,
                x.DurationMonths, x.SessionsPerWeek, x.SessionMinutes, x.TotalSessions, x.RetakeClaimDays,
                x.Price, x.IsPublished, x.Revision, x.SessionType, x.Capacity, x.TargetExamDate }).ToListAsync(ct);
        return rows.Select(x => new
        {
            x.Id, x.Name, x.CertificateLevelId, x.Level, x.DurationMonths, x.SessionsPerWeek, x.SessionMinutes, x.TotalSessions,
            x.RetakeClaimDays, x.Price, x.IsPublished, x.Revision, x.SessionType, x.Capacity, x.TargetExamDate,
            Intensity = IntensityOf(x.SessionsPerWeek),
            IntensityLabel = IntensityLabelOf(x.SessionsPerWeek),
            IsRecommendedIntensity = x.SessionsPerWeek == policy.DefaultSessionsPerWeek,
            TotalTeachingHours = TotalTeachingHoursOf(x.TotalSessions, x.SessionMinutes)
        }).ToList();
    });

    // Cường độ học theo số buổi/tuần (KHÔNG phải cấp độ JLPT).
    internal static string IntensityOf(int sessionsPerWeek) => sessionsPerWeek switch
    {
        2 => "Light", 3 => "Standard", 4 => "Advanced", _ => "Custom"
    };

    internal static string IntensityLabelOf(int sessionsPerWeek) => sessionsPerWeek switch
    {
        2 => "Nhẹ", 3 => "Tiêu chuẩn", 4 => "Nâng cao", _ => "Tùy chỉnh"
    };

    internal static int? SessionsPerWeekOfIntensity(string? intensity) => intensity?.Trim().ToLowerInvariant() switch
    {
        "light" => 2, "standard" => 3, "advanced" => 4, _ => null
    };

    internal static decimal? TotalTeachingHoursOf(int? totalSessions, int? sessionMinutes)
        => totalSessions.HasValue && sessionMinutes.HasValue ? totalSessions.Value * sessionMinutes.Value / 60m : null;

    // Chính sách nhóm hiệu lực; không có bản ghi (môi trường kiểm thử) -> mặc định theo quyết định Product Owner.
    private async Task<MentorGroupPolicy> EffectiveGroupPolicyAsync(DateTime atUtc, CancellationToken ct)
        => (groupPolicies is null ? null : await groupPolicies.GetEffectiveAsync(atUtc, ct)) ?? DefaultGroupPolicy();

    internal static MentorGroupPolicy DefaultGroupPolicy() => new()
    {
        Version = 0, MaxLearners = 4, SessionMinutes = 90, RestMinutes = 15, DailySessionCap = 4, CancellationLeadHours = 24,
        CourseDurationWeeks = 10, AllowedSessionsPerWeekCsv = "2,3,4", DefaultSessionsPerWeek = 3, CheckoutHoldMinutes = 10,
        MinLearnersToStart = 1
    };

    // Gói nhóm chuẩn theo chính sách: tổng buổi = số tuần khóa học x số buổi/tuần.
    private static bool IsPolicyStandardPlan(StudyCoursePlan p, MentorGroupPolicy policy)
        => p.TotalSessions.HasValue && p.TotalSessions.Value == policy.CourseDurationWeeks * p.SessionsPerWeek;

    public Task<ApiResponse<object>> SavePlanAsync(int? id, SaveCoursePlan r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Require(!string.IsNullOrWhiteSpace(r.Name) && r.Name.Length <= 200, "Tên gói phải từ 1 đến 200 ký tự.");
        Require(r.DurationMonths is >= 1 and <= 36 && r.SessionsPerWeek is >= 1 and <= 7, "Kiểm tra thời hạn và số buổi mỗi tuần.");
        Require(r.SessionMinutes is null or >= 15 and <= 240 && r.TotalSessions is null or >= 1 and <= 500
            && r.RetakeClaimDays is null or >= 1 and <= 730, "Thông số thời lượng, tổng buổi hoặc hạn học lại không hợp lệ.");
        Require(r.Price >= 0 && r.Price == decimal.Truncate(r.Price) && r.Price <= int.MaxValue, "Giá phải là số VND nguyên không âm.");
        Require(!r.IsPublished || (r.SessionMinutes.HasValue && r.TotalSessions.HasValue && r.Price > 0),
            "Chưa được mở bán khi thiếu thời lượng, tổng buổi cam kết hoặc giá.");
        Require(await uow.CertificateLevels.Query().Include(x => x.CertificateType)
            .AnyAsync(x => x.Id == r.CertificateLevelId && !x.IsDeleted && !x.CertificateType.IsDeleted && x.CertificateType.IsActive, ct), "Cấp độ không còn hoạt động.");
        Rule(r.SessionType == "Group", "INDIVIDUAL_PLAN_DISCONTINUED", "Hệ thống chỉ hỗ trợ gói khóa học nhóm (Group).", HttpStatusCode.BadRequest);
        // Gói học nhóm: 1 Mentor chính, 1..sức chứa chính sách học viên; 10 tuần x 2/3/4 buổi, buổi 90 phút (theo chính sách).
        var policy = await EffectiveGroupPolicyAsync(DateTime.UtcNow, ct);
        var allowed = policy.AllowedSessionsPerWeek();
        Rule(allowed.Contains(r.SessionsPerWeek), "COURSE_PLAN_INVALID_INTENSITY",
            $"Số buổi mỗi tuần phải là một trong các cường độ được phép: {string.Join(", ", allowed.Select(x => $"{x} buổi ({IntensityLabelOf(x)})"))}.");
        Rule(r.TotalSessions == policy.CourseDurationWeeks * r.SessionsPerWeek && r.SessionMinutes == policy.SessionMinutes,
            "COURSE_PLAN_INVALID_DURATION",
            $"Khóa học nhóm kéo dài {policy.CourseDurationWeeks} tuần: với {r.SessionsPerWeek} buổi/tuần cần đúng {policy.CourseDurationWeeks * r.SessionsPerWeek} buổi, mỗi buổi {policy.SessionMinutes} phút.");
        Rule(r.Capacity >= 1 && r.Capacity <= policy.MaxLearners, "COURSE_PLAN_INVALID_CAPACITY",
            $"Sĩ số lớp nhóm phải từ 1 đến {policy.MaxLearners} học viên.");
        Require(!r.TargetExamDate.HasValue || (r.TargetExamDate.Value.Kind == DateTimeKind.Utc && r.TargetExamDate > DateTime.UtcNow), "Ngày thi mục tiêu phải trong tương lai và có múi giờ UTC.");
        var p = id.HasValue ? await uow.StudyCoursePlans.GetByIdAsync(id.Value, ct) : new StudyCoursePlan();
        Require(p is not null, "Không tìm thấy gói.");
        Require(!id.HasValue || p!.Revision == r.Revision, "Gói vừa được cập nhật. Hãy tải lại.");
        p!.SessionType = r.SessionType; p.Capacity = r.Capacity; p.TargetExamDate = r.TargetExamDate;
        p.Name = r.Name.Trim(); p.CertificateLevelId = r.CertificateLevelId; p.DurationMonths = r.DurationMonths;
        p.SessionsPerWeek = r.SessionsPerWeek; p.SessionMinutes = r.SessionMinutes; p.TotalSessions = r.TotalSessions;
        p.RetakeClaimDays = r.RetakeClaimDays; p.Price = r.Price; p.IsPublished = r.IsPublished;
        if (!id.HasValue) await uow.StudyCoursePlans.AddAsync(p, ct); else p.Revision++;
        await uow.SaveChangesAsync(ct);
        await RecordAdminAuditAsync("StudyCoursePlan", p.Id, id.HasValue ? "Updated" : "Created", new
        {
            p.Id,
            p.Name,
            p.CertificateLevelId,
            p.DurationMonths,
            p.SessionsPerWeek,
            p.SessionMinutes,
            p.TotalSessions,
            p.Capacity,
            p.Price,
            p.IsPublished,
            p.Revision,
        }, ct);
        return new { p.Id };
    });

    public Task<ApiResponse<object>> CoursesAsync(CancellationToken ct) => Run(async () =>
    {
        var actor = Actor;
        var courses = await Details.Where(x => actor.Role == Role.Admin ||
            (actor.Role == Role.Learner && x.LearnerId == actor.Id)
            || (actor.Role == Role.Mentor && (x.MentorId == actor.Id || (x.Cohort != null && x.Cohort.MentorId == actor.Id))))
            .OrderByDescending(x => x.Id).ToListAsync(ct);
        var retaken = await uow.StudyCourses.Query().Where(x => x.RetakeOfId.HasValue).Select(x => x.RetakeOfId!.Value).ToListAsync(ct);
        var cohortCourseIds = courses.Where(c => c.CohortId.HasValue).Select(c => c.Id).ToList();
        var cohortAssignmentsByCourse = cohortCourseIds.Count == 0
            ? new Dictionary<int, List<HomeworkAssignment>>()
            : (await uow.HomeworkAssignments.Query().AsNoTracking()
                .Include(h => h.Submission)
                .Where(h => !h.IsDeleted && h.StudyCourseId.HasValue && cohortCourseIds.Contains(h.StudyCourseId.Value))
                .ToListAsync(ct))
                .GroupBy(h => h.StudyCourseId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
        var fallbackPolicy = cohortCourseIds.Count > 0 && courses.Any(c => c.GroupPolicy is null) && groupPolicies is not null
            ? await groupPolicies.GetEffectiveAsync(DateTime.UtcNow, ct)
            : null;
        return courses.Select(c => View(
            c,
            retaken.Contains(c.Id),
            c.GroupPolicy ?? (c.CohortId.HasValue ? fallbackPolicy : null),
            c.CohortId.HasValue ? cohortAssignmentsByCourse.GetValueOrDefault(c.Id) : null)).ToList();
    });

    public Task<ApiResponse<object>> DetailAsync(int id, CancellationToken ct) => Run(async () =>
    {
        var c = await Details.SingleOrDefaultAsync(x => x.Id == id, ct);
        Require(c is not null, "Không tìm thấy khóa học."); CanRead(c!);
        var effectivePolicy = c!.GroupPolicy;
        List<HomeworkAssignment>? cohortAssignments = null;
        if (c.CohortId.HasValue)
        {
            effectivePolicy ??= groupPolicies is null
                ? null
                : await groupPolicies.GetEffectiveAsync(c.StartsAt ?? c.Cohort?.StartsAt ?? DateTime.UtcNow, ct);
            cohortAssignments = await uow.HomeworkAssignments.Query().AsNoTracking()
                .Include(h => h.Submission)
                .Where(h => !h.IsDeleted && h.StudyCourseId == c.Id)
                .ToListAsync(ct);
        }
        return View(c, await uow.StudyCourses.Query().AnyAsync(x => x.RetakeOfId == id, ct), effectivePolicy, cohortAssignments);
    });

    private static object View(
        StudyCourse c,
        bool retaken,
        MentorGroupPolicy? policy = null,
        IReadOnlyCollection<HomeworkAssignment>? cohortAssignments = null) => new
    {
        c.Id, c.Name, c.PlanId, c.PlanRevision, c.CohortId, c.SessionType, c.TargetExamDate, c.CertificateLevelId, Level = c.CertificateLevel?.Code,
        c.LearnerId, LearnerName = $"{c.Learner?.FirstName} {c.Learner?.LastName}".Trim(), c.MentorId, MentorName = $"{c.Mentor?.FirstName} {c.Mentor?.LastName}".Trim(),
        c.DurationMonths, c.SessionsPerWeek, c.TotalSessions, c.SessionMinutes, c.Amount, c.Currency, c.PaymentStatus,
        c.Status, c.StartsAt, c.EndsAt, c.CompletedAt, c.RetakeOfId, c.RetakeClaimDays, c.CheckoutUrl, c.PaymentExpiresAt,
        c.PaymentConfirmedAtUtc, c.PaymentReviewReason, PaymentState = PaymentStateOf(c, DateTime.UtcNow),
        c.MinimumHomeworkPercent, c.MaximumAbsencePercent,
        Eligibility = CourseRetakePolicy.Evaluate(c, retaken, policy, cohortAssignments),
        Sessions = c.Sessions.OrderBy(x => x.Number).ThenBy(x => x.Attempt).Select(x => new {
            x.Id, x.Number, x.Attempt, x.ScheduledAt, x.Status, x.Note, x.MeetingUrl, x.ConfirmedAt }),
        Homework = c.Homework.OrderBy(x => x.DueAt).Select(x => new {
            x.Id, x.Title, x.Instructions, x.Required, x.DueAt, x.Submission, x.SubmittedAt, x.Passed, x.ReviewedAt, x.Feedback }),
        Requests = c.RetakeRequests.OrderByDescending(x => x.Id).Select(x => new {
            x.Id, x.Status, x.ExamDate, x.ResultPublishedAt, x.Reason, x.SubmittedAt, x.ReviewedAt, x.Response,
            x.EligibilitySnapshot, x.MentorSupportPlan, x.MentorSupportPlanAtUtc }),
        Audit = c.Audit.OrderByDescending(x => x.Id).Select(x => new { x.ActorId, x.Action, x.Detail, x.At })
    };

    public async Task<ApiResponse<object>> EnrollCohortAsync(int cohortId, EnrollCohortRequest r, CancellationToken ct)
    {
        var planId = await uow.StudyCohorts.Query()
            .Where(x => x.Id == cohortId)
            .Select(x => (int?)x.PlanId)
            .SingleOrDefaultAsync(ct);
        if (!planId.HasValue)
            return ApiResponse<object>.Fail("Không tìm thấy lớp học định kỳ.", HttpStatusCode.NotFound);
        return await PurchaseAsync(new PurchaseCourse(planId.Value, r.IdempotencyKey, cohortId, r.EntitlementReference), ct);
    }

    public Task<ApiResponse<object>> PurchaseAsync(PurchaseCourse r, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        Require(r.IdempotencyKey != Guid.Empty, "Thiếu mã giao dịch.");
        StudyCourse? course = null;
        var resumed = false;
        var entitlementMode = new GroupEnrollmentEntitlementDecision(true, RequiresCheckout: true);
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.StudyCourses.LockPurchaseAsync(Actor.Id, r.IdempotencyKey, token);
            course = await uow.StudyCourses.Query().SingleOrDefaultAsync(x => x.LearnerId == Actor.Id && x.PurchaseKey == r.IdempotencyKey, token);
            if (course is not null)
            {
                RequireConflict(course.PlanId == r.PlanId && course.CohortId == r.CohortId, "Mã giao dịch đã dùng cho gói khác.");
                entitlementMode = new GroupEnrollmentEntitlementDecision(true, RequiresCheckout: course.PaymentStatus == "Pending");
                return;
            }
            var p = await uow.StudyCoursePlans.GetByIdAsync(r.PlanId, token);
            Require(p is { IsPublished: true, SessionMinutes: not null, TotalSessions: not null } && p.Price > 0, "Gói chưa đủ cấu hình để mua.");
            Rule(p!.SessionType == "Group" && r.CohortId.HasValue, "INDIVIDUAL_COURSE_DISCONTINUED", "Mua khóa học cá nhân 1-1 đã ngừng hỗ trợ. Vui lòng chọn lớp học nhóm (Cohort).", HttpStatusCode.Conflict);
            Require(!p!.TargetExamDate.HasValue || p.TargetExamDate > DateTime.UtcNow, "Gói đã qua kỳ thi mục tiêu.");
            Require(await uow.CertificateLevels.Query().AnyAsync(x => x.Id == p!.CertificateLevelId && !x.IsDeleted
                && !x.CertificateType.IsDeleted && x.CertificateType.IsActive, token), "Cấp độ không còn mở đăng ký.");
            StudyCohort? cohort = null;
            int? cohortPolicyId = null;
            int? cohortPolicyVersion = null;
            int? cohortCapacitySnapshot = null;
            DateTime? checkoutExpiresAt = null;
            // Thời gian giữ chỗ do Admin cấu hình trong chính sách hiện hành (mặc định 10 phút).
            var holdPolicy = groupPolicies is null ? null : await groupPolicies.GetEffectiveAsync(DateTime.UtcNow, token);
            var holdMinutes = holdPolicy?.CheckoutHoldMinutes is int configuredHold and > 0 ? configuredHold : 10;
            if (p!.SessionType == "Group")
            {
                Require(r.CohortId.HasValue, "Chọn lớp có lịch cố định.");
                await uow.StudyCourses.LockScheduleAsync(Actor.Id, Actor.Id, token);
                await uow.StudyCourses.LockCohortAsync(r.CohortId!.Value, token);
                cohort = await uow.StudyCohorts.Query().Include(x => x.Sessions).ThenInclude(x => x.Availability).ThenInclude(x => x.GroupPolicy)
                    .Include(x => x.Enrollments).SingleOrDefaultAsync(x => x.Id == r.CohortId.Value, token);
                var now = DateTime.UtcNow;
                const string closedMessage = "Lớp đã khóa đăng ký hoặc chưa mở đăng ký.";
                Rule(cohort is not null && cohort.PlanId == p.Id && cohort.Status == "Published",
                    "COHORT_ENROLLMENT_CLOSED", closedMessage, HttpStatusCode.Conflict);
                Rule(EnrollmentCutoffUtc(cohort!) > now, "COHORT_ENROLLMENT_CLOSED", closedMessage, HttpStatusCode.Conflict);
                Rule(!cohort!.Sessions.Any() || cohort.Sessions.Min(s => s.Availability.StartTime) > now,
                    "COHORT_ENROLLMENT_CLOSED", closedMessage, HttpStatusCode.Conflict);
                // Bấm lặp (mã giao dịch mới) khi đơn cũ còn hạn: trả lại đơn đang chờ, không tạo checkout trùng.
                var resumable = cohort.Enrollments
                    .Where(x => x.LearnerId == Actor.Id && x.PlanId == p.Id && x.PaymentStatus == "Pending" && x.PaymentExpiresAt > now)
                    .OrderByDescending(x => x.Id).FirstOrDefault();
                if (resumable is not null)
                {
                    course = resumable;
                    resumed = true;
                    entitlementMode = new GroupEnrollmentEntitlementDecision(true, RequiresCheckout: true);
                    return;
                }
                var policySlots = cohort.Sessions.Where(s => s.Availability.GroupPolicyId.HasValue).Select(s => s.Availability).ToList();
                if (policySlots.Count > 0)
                {
                    var decision = (groupEntitlementContract ?? new GroupEnrollmentEntitlementContract())
                        .EvaluateGroupEnrollment(cohort, p, Actor.Id, r.EntitlementReference);
                    if (!string.IsNullOrWhiteSpace(r.EntitlementReference))
                        Rule(decision.IsAuthorized, "COHORT_ENTITLEMENT_INVALID",
                            decision.ErrorMessage ?? "Mã quyền ghi danh lớp nhóm không hợp lệ.", HttpStatusCode.Conflict);
                    // Có quyền do Admin cấp -> ghi danh trực tiếp; chưa có -> tạo đơn thanh toán PayOS (giá lấy từ gói trong DB).
                    entitlementMode = decision.IsAuthorized
                        ? decision
                        : new GroupEnrollmentEntitlementDecision(true, RequiresCheckout: true);
                    var policy = policySlots[0].GroupPolicy;
                    if (policy is not null)
                    {
                        Rule(cohort.Capacity <= policy.MaxLearners, "COHORT_POLICY_CAPACITY",
                            "Sĩ số lớp vượt giới hạn chính sách hiện hành.", HttpStatusCode.Conflict);
                        cohortPolicyId = policy.Id;
                        cohortPolicyVersion = policy.Version;
                    }
                    else
                    {
                        cohortPolicyId = cohort.GroupPolicyId ?? policySlots[0].GroupPolicyId;
                        cohortPolicyVersion = cohort.PolicyVersionSnapshot;
                    }
                }
                cohortCapacitySnapshot = cohort.CapacitySnapshot ?? cohort.Capacity;
                Rule(cohort.Sessions.All(s => s.Availability.IsApproved && !s.Availability.IsDeleted && s.Availability.RejectedAt == null && s.Availability.ChangeRequestedAt == null),
                    "COHORT_SCHEDULE_PENDING", "Lịch lớp đang chờ xử lý điều chỉnh.", HttpStatusCode.Conflict);
                Rule(cohort.Enrollments.Count(x => ReservesSeat(x, now)) < cohort.Capacity,
                    "COHORT_FULL", "Lớp vừa hết chỗ.", HttpStatusCode.Conflict);
                Rule(!cohort.Enrollments.Any(x => x.LearnerId == Actor.Id && ReservesSeat(x, now)),
                    "COHORT_ALREADY_ENROLLED", "Bạn đã đăng ký hoặc đang giữ chỗ lớp này.", HttpStatusCode.Conflict);
                Rule(cohort.Sessions.Count == p.TotalSessions && cohort.Sessions.All(s => (s.Availability.EndTime - s.Availability.StartTime).TotalMinutes == p.SessionMinutes),
                    "COHORT_PLAN_CHANGED", "Gói đã thay đổi; Admin cần kiểm tra lại lớp.", HttpStatusCode.Conflict);
                Rule(!await HasLearnerConflictAsync(Actor.Id, cohort.Sessions.Select(s => s.Availability).ToList(), 0, token),
                    "COHORT_SCHEDULE_CONFLICT", "Bạn đã có lịch hoặc đang giữ chỗ trùng giờ.", HttpStatusCode.Conflict);
                if (entitlementMode.RequiresCheckout)
                {
                    // Giữ chỗ không được kéo dài quá mốc khóa đăng ký của lớp.
                    var cutoff = EnrollmentCutoffUtc(cohort);
                    Rule(cutoff - now >= TimeSpan.FromMinutes(2), "COHORT_ENROLLMENT_CLOSED",
                        "Lớp sắp khóa đăng ký; không còn đủ thời gian để thanh toán.", HttpStatusCode.Conflict);
                    var hold = now.AddMinutes(holdMinutes);
                    checkoutExpiresAt = hold < cutoff ? hold : cutoff;
                }
            }
            else Require(!r.CohortId.HasValue, "Gói 1–1 không đăng ký vào lớp nhóm.");
            var grantDirectEntitlement = cohort is not null && !entitlementMode.RequiresCheckout;
            course = new StudyCourse { CohortId = cohort?.Id, MentorId = cohort?.MentorId, StartsAt = cohort?.StartsAt, EndsAt = cohort?.EndsAt,
                SessionType = p.SessionType, TargetExamDate = p.TargetExamDate, LearnerId = Actor.Id, PurchaseKey = r.IdempotencyKey,
                PlanId = p!.Id, Name = p.Name, CertificateLevelId = p.CertificateLevelId, PlanRevision = p.Revision,
                DurationMonths = p.DurationMonths, SessionsPerWeek = p.SessionsPerWeek, TotalSessions = p.TotalSessions!.Value,
                SessionMinutes = p.SessionMinutes!.Value, RetakeClaimDays = p.RetakeClaimDays,
                GroupPolicyId = cohortPolicyId ?? cohort?.GroupPolicyId,
                PolicyVersionSnapshot = cohortPolicyVersion ?? cohort?.PolicyVersionSnapshot,
                CapacitySnapshot = cohortCapacitySnapshot,
                Amount = grantDirectEntitlement ? 0 : p.Price,
                Currency = "VND",
                PaymentStatus = grantDirectEntitlement ? "Paid" : "Pending",
                Status = grantDirectEntitlement ? "Active" : "PendingPayment",
                PaymentReference = entitlementMode.EntitlementReference,
                PaymentExpiresAt = grantDirectEntitlement ? null : checkoutExpiresAt ?? DateTime.UtcNow.AddMinutes(holdMinutes),
                OrderSnapshot = cohort is null ? null : CohortOrderSnapshot(cohort, p, cohortPolicyId, cohortPolicyVersion,
                    grantDirectEntitlement ? "AdminEntitlement" : "PayOS", grantDirectEntitlement ? null : holdMinutes) };
            if (grantDirectEntitlement && cohort is not null)
                AddCohortSessions(course, cohort);
            Audit(course, grantDirectEntitlement ? "CohortEnrolled" : "PurchaseCreated",
                grantDirectEntitlement
                    ? $"Ghi danh lớp #{cohort!.Id} theo quyền học được duyệt ({entitlementMode.EntitlementReference ?? "entitlement"})."
                    : "Đóng băng gói. Quyền học lại một lần xét minh chứng nộp BTVN đúng hạn >=90%; không xét tỷ lệ nghỉ.");
            await uow.StudyCourses.AddAsync(course, token); await uow.SaveChangesAsync(token);
            if (grantDirectEntitlement && cohort is not null
                && !string.IsNullOrWhiteSpace(entitlementMode.EntitlementReference)
                && uow.CohortEnrollmentEntitlements is not null)
            {
                var entitlement = await uow.CohortEnrollmentEntitlements.Query()
                    .AsTracking()
                    .Where(x => !x.IsDeleted
                        && x.StudyCohortId == cohort.Id
                        && x.LearnerId == Actor.Id
                        && x.Status == "Active"
                        && x.EntitlementReference == entitlementMode.EntitlementReference)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync(token);
                if (entitlement is not null)
                {
                    entitlement.Status = "Consumed";
                    entitlement.ConsumedAtUtc = DateTime.UtcNow;
                    entitlement.ConsumedByCourseId = course.Id;
                    entitlement.ModifiedDate = DateTime.UtcNow;
                    await uow.SaveChangesAsync(token);
                }
            }
        }, ct);
        RequireConflict(course!.Id < 100_000_000, "Mã đơn vượt giới hạn.");
        RequireConflict(course.PaymentStatus != "Cancelled", "Đơn ghi danh đã hủy; hãy tạo yêu cầu mới.");
        RequireConflict(course.PaymentStatus != "Pending" || course.PaymentExpiresAt > DateTime.UtcNow, "Đơn hết hạn. Hãy tạo đơn mới.");
        if (!entitlementMode.RequiresCheckout)
            return new { course.Id, course.CohortId, course.CheckoutUrl, course.PaymentStatus, course.Status, course.Amount, course.Currency,
                course.PaymentExpiresAt, course.CapacitySnapshot, course.PolicyVersionSnapshot, Resumed = resumed };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            // Đơn được tiếp tục dùng mã giao dịch gốc của nó để tuần tự hóa việc tạo link thanh toán.
            await uow.StudyCourses.LockPurchaseAsync(Actor.Id, course.PurchaseKey, token);
            var state = await uow.StudyCourses.Query().AsNoTracking().Where(x => x.Id == course.Id)
                .Select(x => new { x.PaymentStatus, x.Status, x.CheckoutUrl }).SingleAsync(token);
            course.PaymentStatus = state.PaymentStatus; course.Status = state.Status; course.CheckoutUrl = state.CheckoutUrl;
            if (state.CheckoutUrl is null && state.PaymentStatus == "Pending")
            {
                var root = settings.FrontendBaseUrl.TrimEnd('/');
                // Link PayOS hết hạn cùng lúc với thời gian giữ chỗ.
                var checkout = await payments.CreatePaymentLinkAsync(700_000_000L + course.Id, (int)course.Amount,
                    $"COURSE {course.Id}", root + "/my-courses", root + "/my-courses", course.PaymentExpiresAt, token);
                // Phản hồi rỗng/không hợp lệ: đơn vẫn Pending; thử lại cùng mã giao dịch sẽ tạo lại link.
                Rule(checkout is not null && Uri.TryCreate(checkout.CheckoutUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps,
                    "PAYMENT_GATEWAY_INVALID_LINK", "Cổng thanh toán trả link không hợp lệ. Vui lòng thử lại.", HttpStatusCode.BadGateway);
                course.CheckoutUrl = checkout!.CheckoutUrl;
            }
            await uow.SaveChangesAsync(token);
        }, ct);
        return new { course.Id, course.CohortId, course.CheckoutUrl, course.PaymentStatus, course.Status, course.Amount, course.Currency,
            course.PaymentExpiresAt, course.CapacitySnapshot, course.PolicyVersionSnapshot, Resumed = resumed };
    });

    // Điều khoản đơn hàng bất biến chụp tại thời điểm checkout (giá, lịch, sức chứa, phiên bản chính sách).
    private static string CohortOrderSnapshot(StudyCohort cohort, StudyCoursePlan plan, int? policyId, int? policyVersion, string checkoutKind,
        int? holdMinutes = null)
    {
        var hasSessions = cohort.Sessions.Count > 0;
        return JsonSerializer.Serialize(new
        {
            cohortId = cohort.Id,
            cohortRevision = cohort.Revision,
            cohortName = cohort.Name,
            planId = plan.Id,
            planRevision = plan.Revision,
            price = plan.Price,
            currency = "VND",
            totalSessions = plan.TotalSessions,
            sessionMinutes = plan.SessionMinutes,
            firstSessionUtc = hasSessions ? cohort.Sessions.Min(s => s.Availability.StartTime) : cohort.StartsAt,
            lastSessionUtc = hasSessions ? cohort.Sessions.Max(s => s.Availability.EndTime) : cohort.EndsAt,
            enrollmentCutoffUtc = EnrollmentCutoffUtc(cohort),
            capacity = cohort.Capacity,
            policyId,
            policyVersion,
            mentorId = cohort.MentorId,
            checkoutKind,
            holdMinutes
        });
    }

    public Task<ApiResponse<object>> CancelPendingEnrollmentAsync(int id, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        object result = new { Id = id, Status = "Cancelled" };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var locked = await uow.StudyCourses.LockAsync(id, token);
            Require(locked is not null && locked.CohortId.HasValue && locked.LearnerId == Actor.Id,
                "Không tìm thấy lượt ghi danh của bạn.");
            if (locked!.Status == "Cancelled" && locked.PaymentStatus == "Cancelled") return;
            RequireConflict(locked.PaymentStatus == "Pending", "Đơn đã thanh toán cần Admin xem xét; không tự hủy hoặc hoàn tiền.");
            locked.Status = "Cancelled";
            locked.PaymentStatus = "Cancelled";
            locked.CancelledAtUtc = DateTime.UtcNow;
            locked.CancellationReason = "Hủy giữ chỗ trước khi thanh toán.";
            locked.PaymentExpiresAt = DateTime.UtcNow;
            Audit(locked, "PendingEnrollmentCancelled", "Học viên hủy giữ chỗ trước khi thanh toán; không phát sinh hoàn tiền.");
            await uow.SaveChangesAsync(token);
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> CancelCohortEnrollmentAsync(int id, CancelCohortEnrollmentRequest? request, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Learner or Role.Admin)) throw new UnauthorizedAccessException();
        object result = new { Id = id, Status = "Cancelled" };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var locked = await uow.StudyCourses.LockAsync(id, token);
            Require(locked is not null && locked.CohortId.HasValue
                && (Actor.Role == Role.Admin || locked.LearnerId == Actor.Id),
                "Không tìm thấy lượt ghi danh lớp nhóm.");
            var course = await uow.StudyCourses.Query()
                .Include(x => x.Sessions)
                .Include(x => x.Audit)
                .SingleAsync(x => x.Id == id, token);
            if (course.Status == "Cancelled")
            {
                result = new { course.Id, course.CohortId, course.Status, Idempotent = true };
                return;
            }
            if (course.PaymentStatus == "Pending")
            {
                course.Status = "Cancelled";
                course.PaymentStatus = "Cancelled";
                course.PaymentExpiresAt = DateTime.UtcNow;
                course.CancelledAtUtc = DateTime.UtcNow;
                course.CancellationReason = request?.Reason?.Trim() ?? "Hủy giữ chỗ trước khi thanh toán.";
                Audit(course, "PendingEnrollmentCancelled", course.CancellationReason);
                await uow.SaveChangesAsync(token);
                result = new { course.Id, course.CohortId, course.Status, SeatReleased = true };
                return;
            }
            RequireConflict(course.Status == "Active" && course.PaymentStatus == "Paid",
                "Chỉ được hủy ghi danh đang hoạt động trước giờ khóa.");
            await uow.StudyCourses.LockCohortAsync(course.CohortId!.Value, token);
            var cohort = await uow.StudyCohorts.Query()
                .Include(x => x.Sessions).ThenInclude(x => x.Availability).ThenInclude(x => x.GroupPolicy)
                .Include(x => x.GroupPolicy)
                .SingleAsync(x => x.Id == course.CohortId.Value, token);
            var firstApproved = cohort.Sessions
                .Where(s => s.Availability.IsApproved && !s.Availability.IsDeleted && s.Availability.RejectedAt == null)
                .OrderBy(s => s.Availability.StartTime)
                .Select(s => s.Availability)
                .FirstOrDefault();
            var startsAt = firstApproved?.StartTime ?? cohort.StartsAt;
            var leadHours = firstApproved?.GroupPolicy?.CancellationLeadHours
                ?? cohort.GroupPolicy?.CancellationLeadHours ?? 24;
            var now = DateTime.UtcNow;
            if (Actor.Role != Role.Admin)
            {
                // Quyết định chính thức: Học viên không tự hủy khóa đang diễn ra; Admin xử lý các trường hợp pháp lý/hoàn tiền.
                Rule(startsAt > now, "COHORT_CANCELLATION_NOT_ALLOWED",
                    "Khóa học đã bắt đầu; học viên không thể tự hủy. Vui lòng liên hệ Admin nếu cần hỗ trợ.", HttpStatusCode.Conflict);
                RequireConflict(startsAt > now && (startsAt - now).TotalHours >= leadHours,
                    $"Chỉ được hủy ghi danh trước giờ khai giảng ít nhất {leadHours} giờ.");
            }
            var reasonText = string.IsNullOrWhiteSpace(request?.Reason)
                ? $"Hủy ghi danh cá nhân bởi {Actor.Role}; giải phóng 1 chỗ trong lớp."
                : request!.Reason!.Trim();
            Require(reasonText.Length <= 1000, "Lý do hủy tối đa 1000 ký tự.");
            course.Status = "Cancelled";
            course.CancelledAtUtc = now;
            course.CancellationReason = reasonText;
            foreach (var session in course.Sessions.Where(s => s.Status == "Scheduled"))
            {
                session.Status = "CancelledByLearner";
                session.Note = reasonText;
                session.ConfirmedById = Actor.Id;
                session.ConfirmedAt = now;
            }
            // Không tự hoàn vào sổ cái vé tư vấn 1-1 hoặc hủy lịch của học viên khác.
            Audit(course, "CohortEnrollmentCancelled", reasonText);
            await uow.SaveChangesAsync(token);
            result = new { course.Id, course.CohortId, course.Status, SeatReleased = true };
        }, ct);
        return result;
    });

    public async Task<ApiResponse<object>> ConfirmPaymentAsync(int courseId, PaymentCallbackResult callback, string? reference, CancellationToken ct)
    {
        // This service method is called only after signature verification in PaymentService; it has no public controller route.
        if (!callback.IsValidSignature)
            return ApiResponse<object>.Fail("Chữ ký thanh toán không hợp lệ.", HttpStatusCode.Unauthorized, "PAYMENT_SIGNATURE_INVALID");
        return await Run(() => ApplyVerifiedCoursePaymentAsync(courseId, callback.IsSuccess, callback.Amount,
            callback.OrderCode, reference, "PayOSWebhook", ct));
    }

    // Lõi xử lý thanh toán ĐÃ XÁC MINH (webhook có chữ ký hợp lệ hoặc truy vấn trạng thái server-to-server).
    // Toàn bộ cấp quyền + ghi danh nằm trong một giao dịch: lỗi ở bất kỳ bước nào sẽ hoàn tác cả hai.
    private async Task<object> ApplyVerifiedCoursePaymentAsync(int courseId, bool isSuccess, decimal amount, long orderCode,
        string? reference, string source, CancellationToken ct)
    {
        object result = new { Confirmed = false };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var c = await uow.StudyCourses.LockAsync(courseId, token);
            Require(c is not null, "Không tìm thấy đơn mua khóa.");
            // Ghi danh đích của chuyển lớp ("TRANSFER:{id}"), học lại miễn phí và ghi danh theo quyền Admin cấp không bao giờ là giao dịch PayOS.
            Rule(c!.TransferredFromCourseId is null && c.RetakeOfId is null && c.Amount > 0, "PAYMENT_ORDER_NOT_PURCHASE",
                "Ghi danh này không phải đơn mua qua cổng thanh toán.", HttpStatusCode.Conflict);
            Rule(orderCode == 700_000_000L + courseId, "PAYMENT_ORDER_MISMATCH", "Mã thanh toán không khớp đơn mua.", HttpStatusCode.Conflict);
            Rule(amount == c.Amount, "PAYMENT_AMOUNT_MISMATCH", "Số tiền không khớp đơn mua.", HttpStatusCode.Conflict);
            if (c.PaymentStatus == "Paid")
            {
                // Webhook trùng lặp: không cấp quyền hoặc ghi danh lần hai.
                result = new { Confirmed = true, Idempotent = true, c.Status };
                return;
            }
            var now = DateTime.UtcNow;
            if (!isSuccess)
            {
                if (c.PaymentStatus == "Pending")
                {
                    c.PaymentStatus = "Failed"; c.Status = "PaymentFailed"; c.PaymentExpiresAt = now;
                    Audit(c, "PaymentFailed", $"Cổng thanh toán báo giao dịch không thành công ({source}); giải phóng giữ chỗ.");
                    await uow.SaveChangesAsync(token);
                }
                result = new { Confirmed = false, Idempotent = false, c.PaymentStatus, c.Status };
                return;
            }
            var previousStatus = c.Status;
            var previousPaymentStatus = c.PaymentStatus;
            c.PaymentStatus = "Paid";
            if (!string.IsNullOrWhiteSpace(reference))
                c.ProviderPaymentReference = reference.Length > 120 ? reference[..120] : reference;
            c.PaymentConfirmedAtUtc = now;
            c.PaymentReference = reference ?? c.PaymentReference;
            // A valid paid webhook may arrive late. The checkout timer must never discard received money.
            if (previousStatus is "Cancelled" or "PaymentFailed" or "PaymentExpired"
                || previousPaymentStatus is "Cancelled" or "Failed" or "Expired")
            {
                c.Status = "PaymentReview";
                c.PaymentReviewReason = "LATE_PAYMENT_AFTER_CANCEL_OR_FAILURE";
                Audit(c, "CancelledEnrollmentPaidLate", $"Đã nhận tiền sau khi đơn bị hủy/thất bại/hết hạn ({source}); Admin xử lý, không tự kích hoạt quyền học.");
                await QueuePaymentReviewNotificationsAsync(c, token);
                await uow.SaveChangesAsync(token);
                result = new { Confirmed = true, Idempotent = false, c.Status, c.PaymentReviewReason };
                return;
            }
            c.Status = "Ready";
            if (c.CohortId.HasValue)
            {
                await uow.StudyCourses.LockScheduleAsync(c.LearnerId, c.LearnerId, token);
                await uow.StudyCourses.LockCohortAsync(c.CohortId.Value, token);
                var cohort = await uow.StudyCohorts.Query().Include(x => x.Plan).Include(x => x.Enrollments)
                    .Include(x => x.Sessions).ThenInclude(x => x.Availability).ThenInclude(x => x.GroupPolicy)
                    .SingleAsync(x => x.Id == c.CohortId.Value, token);
                var alreadyStarted = cohort.Sessions.Any() && cohort.Sessions.Min(s => s.Availability.StartTime) <= now;
                string? reviewReason = null;
                if (!(c.PaymentExpiresAt > now)) reviewReason = "RESERVATION_EXPIRED";
                else if (cohort.Status != "Published") reviewReason = "COHORT_NOT_PUBLISHED";
                else if (EnrollmentCutoffUtc(cohort) <= now) reviewReason = "ENROLLMENT_CUTOFF_PASSED";
                else if (alreadyStarted) reviewReason = "COHORT_ALREADY_STARTED";
                else if (cohort.Enrollments.Any(x => x.Id != c.Id && x.LearnerId == c.LearnerId && ReservesSeat(x, now)))
                    reviewReason = "DUPLICATE_ENROLLMENT";
                else if (cohort.Enrollments.Count(x => x.Id != c.Id && ReservesSeat(x, now)) >= cohort.Capacity)
                    reviewReason = "COHORT_FULL";
                else if (await HasLearnerConflictAsync(c.LearnerId, cohort.Sessions.Select(s => s.Availability).ToList(), c.Id, token))
                    reviewReason = "SCHEDULE_CONFLICT";
                if (reviewReason is not null)
                {
                    // Keep the money recorded, never overbook or silently discard a late successful payment.
                    c.Status = "PaymentReview";
                    c.PaymentReviewReason = reviewReason;
                    Audit(c, "PaidEnrollmentUnresolved",
                        $"Đã nhận tiền ({source}) nhưng suất không còn hợp lệ: {reviewReason}. Không cấp quyền học; Admin xử lý hoàn tiền/đổi lớp.");
                    await QueuePaymentReviewNotificationsAsync(c, token);
                }
                else
                {
                    // Cấp quyền từ giao dịch đã xác minh và tiêu thụ ngay; mã tham chiếu duy nhất chặn cấp trùng.
                    if (uow.CohortEnrollmentEntitlements is not null)
                    {
                        var note = $"PayOS order {orderCode}; ref {reference}";
                        await uow.CohortEnrollmentEntitlements.AddAsync(new CohortEnrollmentEntitlement
                        {
                            StudyCohortId = cohort.Id,
                            PlanId = cohort.PlanId,
                            LearnerId = c.LearnerId,
                            SourceType = "VerifiedCoursePurchase",
                            EntitlementReference = $"PURCHASE-{c.Id}",
                            Status = "Consumed",
                            ConsumedAtUtc = now,
                            ConsumedByCourseId = c.Id,
                            GrantedAtUtc = now,
                            Note = note.Length > 500 ? note[..500] : note
                        }, token);
                    }
                    AddCohortSessions(c, cohort);
                    Audit(c, "CohortPurchaseConfirmed",
                        $"Xác nhận thanh toán đơn PayOS {orderCode} ({source}); ghi danh lớp #{cohort.Id} và cấp quyền PURCHASE-{c.Id}.");
                }
            }
            await uow.SaveChangesAsync(token);
            result = new { Confirmed = true, Idempotent = false, c.Status, c.PaymentReviewReason };
        }, ct);
        return result;
    }

    public Task<ApiResponse<object>> ReconcileCoursePaymentAsync(int courseId, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Learner or Role.Admin)) throw new UnauthorizedAccessException();
        var c = await uow.StudyCourses.Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct);
        Require(c is not null, "Không tìm thấy khóa học.", HttpStatusCode.NotFound);
        if (Actor.Role == Role.Learner && c!.LearnerId != Actor.Id) throw new UnauthorizedAccessException();
        if (c!.PaymentStatus != "Pending" || c.Amount <= 0) return CoursePaymentState(c, false);
        // Chỉ tin trạng thái truy vấn server-to-server có xác thực từ PayOS; redirect phía client không phải bằng chứng.
        var orderCode = 700_000_000L + courseId;
        PaymentLinkInformation info;
        try { info = await payments.GetPaymentInformationAsync(orderCode, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return CoursePaymentState(c, true); }
        var remoteStatus = info.Status?.Trim().ToUpperInvariant();
        if (remoteStatus == "PAID" && info.AmountPaid >= c.Amount)
            await ApplyVerifiedCoursePaymentAsync(courseId, true, info.Amount, info.OrderCode, info.PaymentLinkId, "PayOSStatusQuery", ct);
        else if (remoteStatus == "CANCELLED")
            await ApplyVerifiedCoursePaymentAsync(courseId, false, info.Amount, info.OrderCode, info.PaymentLinkId, "PayOSStatusQuery", ct);
        else if (remoteStatus == "EXPIRED")
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                var locked = await uow.StudyCourses.LockAsync(courseId, token);
                if (locked is null || locked.PaymentStatus != "Pending") return;
                var now = DateTime.UtcNow;
                locked.PaymentStatus = "Expired";
                locked.Status = "PaymentExpired";
                if (!(locked.PaymentExpiresAt <= now)) locked.PaymentExpiresAt = now;
                Audit(locked, "PaymentExpired", "PayOS xác nhận link thanh toán đã hết hạn; giải phóng giữ chỗ.");
                await uow.SaveChangesAsync(token);
            }, ct);
        }
        var current = await uow.StudyCourses.Query().AsNoTracking().SingleAsync(x => x.Id == courseId, ct);
        return CoursePaymentState(current, false);
    });

    private static object CoursePaymentState(StudyCourse c, bool gatewayUnavailable) => new
    {
        c.Id, c.CohortId, c.Amount, c.Currency, c.PaymentStatus, c.Status, c.CheckoutUrl, c.PaymentExpiresAt,
        c.PaymentConfirmedAtUtc, c.PaymentReviewReason, c.ProviderPaymentReference,
        PaymentState = PaymentStateOf(c, DateTime.UtcNow),
        GatewayUnavailable = gatewayUnavailable
    };

    public Task<ApiResponse<object>> AttendanceAsync(int id, int sessionId, ConfirmCourseAttendance r, CancellationToken ct) => Run(async () =>
    {
        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var locked = await uow.StudyCourses.LockAsync(id, token);
            Require(locked is not null, "Không tìm thấy khóa học.");
            var c = await Details.SingleAsync(x => x.Id == id, token);
            var target = c.Sessions.SingleOrDefault(x => x.Id == sessionId);
            if (Actor.Role != Role.Admin)
            {
                // Mentor thực dạy của buổi (theo khung giờ) chốt điểm danh; sau khi thay Mentor chính,
                // buổi cũ vẫn do Mentor cũ chốt, buổi mới do Mentor mới chốt.
                if (Actor.Role != Role.Mentor) throw new UnauthorizedAccessException();
                int? slotMentorId = target?.AvailabilityId is int slotId
                    ? await uow.MentorAvailabilities.Query().Where(a => a.Id == slotId).Select(a => (int?)a.MentorId).SingleOrDefaultAsync(token)
                    : null;
                if ((slotMentorId ?? c.MentorId ?? c.Cohort?.MentorId) != Actor.Id) throw new UnauthorizedAccessException();
            }
            // Ghi danh đã chuyển lớp vẫn chốt được các buổi diễn ra trước thời điểm chuyển.
            Require((c.Status == "Active" && c.PaymentStatus == "Paid")
                || (c.Status == "Transferred" && c.TransferredAtUtc.HasValue && target is not null && target.ScheduledAt < c.TransferredAtUtc),
                "Khóa chưa hoạt động hoặc đã chốt dữ liệu.");
            result = await ConfirmAttendance(c, sessionId, r);
            await uow.SaveChangesAsync(token);
        }, ct);
        return result;
    });

    private Task<object> ConfirmAttendance(StudyCourse c, int sessionId, ConfirmCourseAttendance r)
    {
        var session = c.Sessions.SingleOrDefault(x => x.Id == sessionId);
        Require(session is not null && session.Status == "Scheduled", "Buổi đã được chốt hoặc không tồn tại.");
        Require(r.Status is "Present" or "Absent" or "ExcusedAbsent" or "CancelledByMentor", "Trạng thái điểm danh không hợp lệ.");
        Require(!string.IsNullOrWhiteSpace(r.Note) && r.Note.Length <= 1000, "Cần ghi lý do/xác nhận, tối đa 1000 ký tự.");
        Require(r.Status == "CancelledByMentor" || session!.ScheduledAt.AddMinutes(c.SessionMinutes) <= DateTime.UtcNow, "Chưa được xác nhận hoàn thành trước giờ kết thúc buổi học.");
        session!.Status = r.Status; session.Note = r.Note.Trim(); session.ConfirmedById = Actor.Id; session.ConfirmedAt = DateTime.UtcNow;
        Audit(c, "Attendance", $"Buổi {session.Number}/{session.Attempt}: {r.Status}. {r.Note.Trim()}");
        return Task.FromResult<object>(new { session.Id });
    }

    public Task<ApiResponse<object>> CompleteAsync(int id, CancellationToken ct) => Change(id, c =>
    {
        Admin(); Active(c);
        Require(c.Sessions.Where(x => x.Status != "CancelledByMentor").Select(x => x.Number).Distinct().Count() == c.TotalSessions && !c.Sessions.Any(x => x.Status == "Scheduled"), "Cần chốt đủ các buổi cam kết trước khi hoàn thành khóa.");
        // Homework is deferred; completion records actual teaching sessions only.
        c.Status = "Completed"; c.CompletedAt = DateTime.UtcNow;
        Audit(c, "Completed", "Xác nhận các buổi đã hoàn thành; chưa tự động xét BTVN/học lại.");
        return Task.FromResult<object>(new { c.Id });
    }, ct);

    public Task<ApiResponse<object>> RequestRetakeAsync(int id, DateTime examDate, DateTime resultPublishedAt, IFormFile? evidence, CancellationToken ct, string? reason = null) => Change(id, async c =>
    {
        Learner(c);
        var normalizedReason = reason?.Trim() ?? string.Empty;
        Require(normalizedReason.Length <= 1000, "Lý do yêu cầu ôn lại tối đa 1000 ký tự.");
        if (c.CohortId.HasValue)
            Require(!string.IsNullOrWhiteSpace(normalizedReason), "Vui lòng nhập lý do/tường trình kết quả thi JLPT chính thức.");
        var effectivePolicy = c.GroupPolicy ?? (groupPolicies is null ? null : await groupPolicies.GetEffectiveAsync(c.StartsAt ?? c.Cohort?.StartsAt ?? DateTime.UtcNow, ct));
        var cohortAssignments = c.CohortId.HasValue
            ? await uow.HomeworkAssignments.Query().AsNoTracking()
                .Include(h => h.Submission)
                .Where(h => !h.IsDeleted && h.StudyCourseId == c.Id)
                .ToListAsync(ct)
            : [];
        var eligibility = CourseRetakePolicy.Evaluate(c, await uow.StudyCourses.Query().AnyAsync(x => x.RetakeOfId == id, ct), effectivePolicy, cohortAssignments);
        Require(c.Status == "Completed" && c.PaymentStatus == "Paid" && c.RetakeOfId is null
            && !await uow.StudyCourses.Query().AnyAsync(x => x.RetakeOfId == id, ct), "Khóa chưa hoàn thành hoặc đã dùng quyền học lại.");
        Require(!c.RetakeRequests.Any(x => x.Status == "Pending"), "Đã có yêu cầu đang chờ duyệt.");
        var courseStartDate = (c.StartsAt ?? c.Cohort?.StartsAt)?.Date;
        Require(courseStartDate.HasValue && examDate.Kind == DateTimeKind.Utc && resultPublishedAt.Kind == DateTimeKind.Utc
            && examDate.Date >= courseStartDate.Value && examDate <= resultPublishedAt && resultPublishedAt <= DateTime.UtcNow, "Kiểm tra ngày thi và ngày công bố kết quả JLPT.");
        Require(!c.RetakeClaimDays.HasValue || DateTime.UtcNow <= resultPublishedAt.AddDays(c.RetakeClaimDays.Value), "Đã quá hạn gửi yêu cầu theo chính sách khi mua.");
        var requireOfficialEvidence = effectivePolicy?.RequireOfficialExamEvidence ?? true;
        string evidenceUrl = string.Empty;
        if (requireOfficialEvidence || evidence is not null)
        {
            Require(evidence is not null && evidence.Length is > 0 and <= 10 * 1024 * 1024
                && evidence.ContentType is "application/pdf" or "image/png" or "image/jpeg", "Minh chứng phải là PDF/PNG/JPG tối đa 10 MB.");
            await using var stream = evidence!.OpenReadStream();
            var file = await storage.UploadPrivateVerificationAsync(stream, evidence.ContentType, Actor.Id, ct);
            evidenceUrl = file.Url;
        }
        var retakeRequest = new CourseRetakeRequest
        {
            ExamDate = examDate,
            ResultPublishedAt = resultPublishedAt,
            EvidenceUrl = evidenceUrl,
            Reason = normalizedReason,
            EligibilitySnapshot = JsonSerializer.Serialize(eligibility)
        };
        c.RetakeRequests.Add(retakeRequest);
        var requireAdminReview = effectivePolicy?.RequireAdminRemediationReview ?? true;
        if (!requireAdminReview && eligibility.CanClaim && c.SessionType == "Individual")
        {
            retakeRequest.Status = "Approved";
            retakeRequest.ReviewedAt = DateTime.UtcNow;
            retakeRequest.Response = "Tự động duyệt theo chính sách học lại.";
            var retake = new StudyCourse
            {
                LearnerId = c.LearnerId, PurchaseKey = Guid.NewGuid(), PlanId = c.PlanId, RetakeOfId = c.Id,
                SessionType = c.SessionType, Name = c.Name, CertificateLevelId = c.CertificateLevelId, PlanRevision = c.PlanRevision,
                DurationMonths = c.DurationMonths, SessionsPerWeek = c.SessionsPerWeek, SessionMinutes = c.SessionMinutes,
                TotalSessions = c.TotalSessions, RetakeClaimDays = c.RetakeClaimDays, MinimumHomeworkPercent = c.MinimumHomeworkPercent,
                MaximumAbsencePercent = c.MaximumAbsencePercent, GroupPolicyId = effectivePolicy?.Id,
                PolicyVersionSnapshot = effectivePolicy?.Version, Amount = 0, PaymentStatus = "Paid", Status = "Ready"
            };
            Audit(retake, "FreeRetakeAutoApproved", $"Học lại một lần của khóa {c.Id} theo chính sách không cần duyệt Admin.");
            await uow.StudyCourses.AddAsync(retake, ct);
            await uow.SaveChangesAsync(ct);
            Audit(c, "RetakeAutoApproved", "Đủ điều kiện học lại theo chính sách hiện hành.");
            return new { c.Id, NewCourseId = retake.Id, AutoApproved = true };
        }
        Audit(c, "RetakeRequested", "Học viên khai báo trượt JLPT chính thức; chờ Admin kiểm tra danh tính, cấp độ và kết quả.");
        return new { c.Id, AutoApproved = false };
    }, ct);

    public Task<ApiResponse<object>> ReviewRetakeAsync(int id, int requestId, ReviewCourseRetake r, CancellationToken ct) => Change(id, async c =>
    {
        Admin();
        var request = c.RetakeRequests.SingleOrDefault(x => x.Id == requestId);
        Require(request is { Status: "Pending" }, "Yêu cầu đã xử lý hoặc không tồn tại.");
        Require(!string.IsNullOrWhiteSpace(r.Response) && r.Response.Length <= 2000, "Cần ghi lý do/quyết định, tối đa 2000 ký tự.");
        int? newCourseId = null;
        if (r.Approve)
        {
            var effectivePolicy = c.GroupPolicy ?? (c.GroupPolicyId.HasValue && groupPolicies is not null
                ? await groupPolicies.GetEffectiveAsync(c.StartsAt ?? c.Cohort?.StartsAt ?? DateTime.UtcNow, ct)
                : null);
            var cohortAssignments = c.CohortId.HasValue
                ? await uow.HomeworkAssignments.Query().AsNoTracking()
                    .Include(h => h.Submission)
                    .Where(h => !h.IsDeleted && h.StudyCourseId == c.Id)
                    .ToListAsync(ct)
                : [];
            var eligibility = CourseRetakePolicy.Evaluate(c, false, effectivePolicy, cohortAssignments);
            var requireOfficial = effectivePolicy?.RequireOfficialExamEvidence ?? true;
            if (requireOfficial)
                Require(r.OfficialFailureVerified, "Admin phải xác nhận minh chứng trượt JLPT, đúng học viên và cấp độ của khóa.");
            Require(c.Status == "Completed" && c.PaymentStatus == "Paid" && c.RetakeOfId is null
                && !await uow.StudyCourses.Query().AnyAsync(x => x.RetakeOfId == id, ct), "Khóa không còn quyền học lại.");
            if (effectivePolicy is null)
            {
                Require(r.OnTimeHomeworkEvidenceVerified, "Admin cần xác nhận minh chứng ít nhất 90% BTVN nộp đúng hạn; hệ thống BTVN chưa triển khai.");
            }
            else
            {
                Require(eligibility.HomeworkMet || r.OnTimeHomeworkEvidenceVerified,
                    $"Học viên chưa đạt điều kiện BTVN nộp đúng hạn ({effectivePolicy.RemediationThresholdComparison} {effectivePolicy.RemediationCompletionThresholdPercent:0.##}%).");
            }
            var retake = new StudyCourse { LearnerId = c.LearnerId, PurchaseKey = Guid.NewGuid(), PlanId = c.PlanId,
                RetakeOfId = c.Id, SessionType = c.SessionType, Name = c.Name, CertificateLevelId = c.CertificateLevelId, PlanRevision = c.PlanRevision,
                DurationMonths = c.DurationMonths, SessionsPerWeek = c.SessionsPerWeek, SessionMinutes = c.SessionMinutes,
                TotalSessions = c.TotalSessions, RetakeClaimDays = c.RetakeClaimDays, MinimumHomeworkPercent = c.MinimumHomeworkPercent,
                MaximumAbsencePercent = c.MaximumAbsencePercent, GroupPolicyId = effectivePolicy?.Id,
                PolicyVersionSnapshot = effectivePolicy?.Version, Amount = 0, PaymentStatus = "Paid", Status = "Ready" };
            if (c.SessionType == "Group")
            {
                Require(r.CohortId.HasValue, "Chọn lớp đợt kế tiếp cho gói học lại nhóm.");
                await uow.StudyCourses.LockCohortAsync(r.CohortId!.Value, ct);
                var cohort = await uow.StudyCohorts.Query().Include(x => x.Plan).Include(x => x.Enrollments)
                    .Include(x => x.Sessions).ThenInclude(x => x.Availability).SingleOrDefaultAsync(x => x.Id == r.CohortId.Value, ct);
                Require(cohort is not null && cohort.Status == "Published" && cohort.StartsAt > DateTime.UtcNow
                    && cohort.Plan.CertificateLevelId == c.CertificateLevelId && cohort.Plan.TotalSessions >= c.TotalSessions
                    && cohort.Plan.SessionMinutes >= c.SessionMinutes
                    && cohort.Enrollments.Count(x => ReservesSeat(x, DateTime.UtcNow)) < cohort.Capacity,
                    "Lớp học lại phải còn chỗ, cùng cấp độ, đủ số buổi/thời lượng tương đương và chưa khai giảng.");
                retake.CohortId = cohort!.Id; retake.PlanId = cohort.PlanId; retake.MentorId = cohort.MentorId;
                retake.StartsAt = cohort.StartsAt; retake.EndsAt = cohort.EndsAt; retake.CapacitySnapshot = cohort.CapacitySnapshot ?? cohort.Capacity;
                retake.TotalSessions = cohort.Sessions.Count; retake.SessionMinutes = cohort.Plan.SessionMinutes!.Value;
                AddCohortSessions(retake, cohort);
            }
            Audit(retake, "FreeRetakeCreated", $"Học lại một lần của khóa {c.Id}; không phát sinh quyền học lại tiếp.");
            await uow.StudyCourses.AddAsync(retake, ct); await uow.SaveChangesAsync(ct); newCourseId = retake.Id;
        }
        request!.Status = r.Approve ? "Approved" : "Rejected"; request.Response = r.Response.Trim();
        request.ReviewedById = Actor.Id; request.ReviewedAt = DateTime.UtcNow;
        Audit(c, "RetakeReviewed", $"Yêu cầu {requestId}: {request.Status}. {r.Response.Trim()}");
        return new { NewCourseId = newCourseId };
    }, ct);

    public Task<ApiResponse<object>> SaveRetakeSupportPlanAsync(int id, int requestId, SaveRetakeSupportPlan r, CancellationToken ct) => Change(id, c =>
    {
        Teacher(c);
        var request = c.RetakeRequests.SingleOrDefault(x => x.Id == requestId);
        RequireConflict(request is not null, "Không tìm thấy yêu cầu hỗ trợ ôn lại JLPT.");
        RequireConflict(request!.Status == "Approved", "Chỉ cố vấn mới lập kế hoạch hỗ trợ sau khi Admin đã duyệt yêu cầu.");
        var plan = r.SupportPlan?.Trim() ?? string.Empty;
        Require(!string.IsNullOrWhiteSpace(plan) && plan.Length <= 2000, "Cần nhập lộ trình hỗ trợ ôn lại tối đa 2000 ký tự.");
        request.MentorSupportPlan = plan;
        request.MentorSupportPlanById = Actor.Id;
        request.MentorSupportPlanAtUtc = DateTime.UtcNow;
        Audit(c, "RetakeSupportPlanSaved", $"Mentor đã lập kế hoạch hỗ trợ ôn lại cho yêu cầu #{requestId}.");
        return Task.FromResult<object>(new
        {
            request.Id,
            request.Status,
            request.MentorSupportPlan,
            request.MentorSupportPlanAtUtc
        });
    }, ct);

    public async Task<PrivateVerificationReadResult?> EvidenceAsync(int id, int requestId, CancellationToken ct)
    {
        var c = await Details.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (c is null || (Actor.Role != Role.Admin && !(Actor.Role == Role.Learner && c.LearnerId == Actor.Id))) return null;
        var r = c.RetakeRequests.SingleOrDefault(x => x.Id == requestId);
        return r is null ? null : await storage.OpenPrivateVerificationAsync(r.EvidenceUrl, ct);
    }
}
