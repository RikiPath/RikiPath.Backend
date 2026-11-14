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
    IFileStorageService storage, AppSettings settings) : IStudyCourseService
{
    private ClaimDTO Actor => claims.GetUserClaim();
    private sealed class CourseRuleException(string message) : Exception(message);
    private static void Require(bool condition, string message) { if (!condition) throw new CourseRuleException(message); }
    private void Admin() { if (Actor.Role != Role.Admin) throw new UnauthorizedAccessException(); }
    private void Learner(StudyCourse c) { if (Actor.Role != Role.Learner || c.LearnerId != Actor.Id) throw new UnauthorizedAccessException(); }
    private void Teacher(StudyCourse c) { if (Actor.Role != Role.Mentor || c.MentorId != Actor.Id) throw new UnauthorizedAccessException(); }
    private void CanRead(StudyCourse c)
    {
        if (Actor.Role != Role.Admin && !(Actor.Role == Role.Learner && c.LearnerId == Actor.Id)
            && !(Actor.Role == Role.Mentor && c.MentorId == Actor.Id)) throw new UnauthorizedAccessException();
    }
    private IQueryable<StudyCourse> Details => uow.StudyCourses.Query().Include(x => x.CertificateLevel)
        .Include(x => x.Learner).Include(x => x.Mentor).Include(x => x.Sessions).Include(x => x.Homework)
        .Include(x => x.RetakeRequests).Include(x => x.Audit).AsSplitQuery();
    private void Audit(StudyCourse c, string action, string detail)
        => c.Audit.Add(new() { ActorId = Actor.Id, Action = action, Detail = detail });
    private static void Active(StudyCourse c) => Require(c.Status == "Active" && c.PaymentStatus == "Paid", "Khóa chưa hoạt động hoặc đã chốt dữ liệu.");

    private async Task<ApiResponse<object>> Run(Func<Task<object>> action)
    {
        try { return ApiResponse<object>.Success(await action()); }
        catch (UnauthorizedAccessException) { return ApiResponse<object>.Fail("Bạn không có quyền thực hiện thao tác này.", HttpStatusCode.Forbidden); }
        catch (CourseRuleException ex) { return ApiResponse<object>.Fail(ex.Message); }
        catch (DbUpdateConcurrencyException) { return ApiResponse<object>.Fail("Dữ liệu vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict); }
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
        await uow.StudyCoursePlans.Query().Include(x => x.CertificateLevel)
            .Where(x => Actor.Role == Role.Admin || (x.IsPublished && !x.CertificateLevel.IsDeleted && !x.CertificateLevel.CertificateType.IsDeleted && x.CertificateLevel.CertificateType.IsActive))
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.CertificateLevelId, Level = x.CertificateLevel.Code,
                x.DurationMonths, x.SessionsPerWeek, x.SessionMinutes, x.TotalSessions, x.RetakeClaimDays,
                x.Price, x.IsPublished, x.Revision }).ToListAsync(ct));

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
        var p = id.HasValue ? await uow.StudyCoursePlans.GetByIdAsync(id.Value, ct) : new StudyCoursePlan();
        Require(p is not null, "Không tìm thấy gói.");
        Require(!id.HasValue || p!.Revision == r.Revision, "Gói vừa được cập nhật. Hãy tải lại.");
        p!.Name = r.Name.Trim(); p.CertificateLevelId = r.CertificateLevelId; p.DurationMonths = r.DurationMonths;
        p.SessionsPerWeek = r.SessionsPerWeek; p.SessionMinutes = r.SessionMinutes; p.TotalSessions = r.TotalSessions;
        p.RetakeClaimDays = r.RetakeClaimDays; p.Price = r.Price; p.IsPublished = r.IsPublished;
        if (!id.HasValue) await uow.StudyCoursePlans.AddAsync(p, ct); else p.Revision++;
        await uow.SaveChangesAsync(ct);
        return new { p.Id };
    });

    public Task<ApiResponse<object>> CoursesAsync(CancellationToken ct) => Run(async () =>
    {
        var actor = Actor;
        var courses = await Details.Where(x => actor.Role == Role.Admin ||
            (actor.Role == Role.Learner && x.LearnerId == actor.Id) || (actor.Role == Role.Mentor && x.MentorId == actor.Id))
            .OrderByDescending(x => x.Id).ToListAsync(ct);
        var retaken = await uow.StudyCourses.Query().Where(x => x.RetakeOfId.HasValue).Select(x => x.RetakeOfId!.Value).ToListAsync(ct);
        return courses.Select(c => View(c, retaken.Contains(c.Id))).ToList();
    });

    public Task<ApiResponse<object>> DetailAsync(int id, CancellationToken ct) => Run(async () =>
    {
        var c = await Details.SingleOrDefaultAsync(x => x.Id == id, ct);
        Require(c is not null, "Không tìm thấy khóa học."); CanRead(c!);
        return View(c!, await uow.StudyCourses.Query().AnyAsync(x => x.RetakeOfId == id, ct));
    });

    private static object View(StudyCourse c, bool retaken) => new
    {
        c.Id, c.Name, c.PlanId, c.PlanRevision, c.CertificateLevelId, Level = c.CertificateLevel?.Code,
        c.LearnerId, LearnerName = $"{c.Learner?.FirstName} {c.Learner?.LastName}".Trim(), c.MentorId, MentorName = $"{c.Mentor?.FirstName} {c.Mentor?.LastName}".Trim(),
        c.DurationMonths, c.SessionsPerWeek, c.TotalSessions, c.SessionMinutes, c.Amount, c.PaymentStatus,
        c.Status, c.StartsAt, c.EndsAt, c.CompletedAt, c.RetakeOfId, c.RetakeClaimDays, c.CheckoutUrl, c.PaymentExpiresAt,
        c.MinimumHomeworkPercent, c.MaximumAbsencePercent,
        Eligibility = CourseRetakePolicy.Evaluate(c, retaken),
        Sessions = c.Sessions.OrderBy(x => x.Number).ThenBy(x => x.Attempt).Select(x => new {
            x.Id, x.Number, x.Attempt, x.ScheduledAt, x.Status, x.Note, x.MeetingUrl, x.ConfirmedAt }),
        Homework = c.Homework.OrderBy(x => x.DueAt).Select(x => new {
            x.Id, x.Title, x.Instructions, x.Required, x.DueAt, x.Submission, x.SubmittedAt, x.Passed, x.ReviewedAt, x.Feedback }),
        Requests = c.RetakeRequests.OrderByDescending(x => x.Id).Select(x => new {
            x.Id, x.Status, x.ExamDate, x.ResultPublishedAt, x.SubmittedAt, x.ReviewedAt, x.Response }),
        Audit = c.Audit.OrderByDescending(x => x.Id).Select(x => new { x.ActorId, x.Action, x.Detail, x.At })
    };

    public Task<ApiResponse<object>> PurchaseAsync(PurchaseCourse r, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        Require(r.IdempotencyKey != Guid.Empty, "Thiếu mã giao dịch.");
        StudyCourse? course = null;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.StudyCourses.LockPurchaseAsync(Actor.Id, r.IdempotencyKey, token);
            course = await uow.StudyCourses.Query().SingleOrDefaultAsync(x => x.LearnerId == Actor.Id && x.PurchaseKey == r.IdempotencyKey, token);
            if (course is not null) { Require(course.PlanId == r.PlanId, "Mã giao dịch đã dùng cho gói khác."); return; }
            var p = await uow.StudyCoursePlans.GetByIdAsync(r.PlanId, token);
            Require(p is { IsPublished: true, SessionMinutes: not null, TotalSessions: not null } && p.Price > 0, "Gói chưa đủ cấu hình để mua.");
            Require(await uow.CertificateLevels.Query().AnyAsync(x => x.Id == p!.CertificateLevelId && !x.IsDeleted
                && !x.CertificateType.IsDeleted && x.CertificateType.IsActive, token), "Cấp độ không còn mở đăng ký.");
            course = new StudyCourse { LearnerId = Actor.Id, PurchaseKey = r.IdempotencyKey,
                PlanId = p!.Id, Name = p.Name, CertificateLevelId = p.CertificateLevelId, PlanRevision = p.Revision,
                DurationMonths = p.DurationMonths, SessionsPerWeek = p.SessionsPerWeek, TotalSessions = p.TotalSessions!.Value,
                SessionMinutes = p.SessionMinutes!.Value, RetakeClaimDays = p.RetakeClaimDays,
                Amount = p.Price, PaymentExpiresAt = DateTime.UtcNow.AddMinutes(15) };
            Audit(course, "PurchaseCreated", "Đóng băng gói và chính sách 90% BTVN / tối đa 20% nghỉ.");
            await uow.StudyCourses.AddAsync(course, token); await uow.SaveChangesAsync(token);
        }, ct);
        Require(course!.Id < 100_000_000, "Mã đơn vượt giới hạn.");
        Require(course.PaymentStatus != "Pending" || course.PaymentExpiresAt > DateTime.UtcNow, "Đơn hết hạn. Hãy tạo đơn mới.");
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.StudyCourses.LockPurchaseAsync(Actor.Id, r.IdempotencyKey, token);
            var state = await uow.StudyCourses.Query().AsNoTracking().Where(x => x.Id == course.Id)
                .Select(x => new { x.PaymentStatus, x.Status, x.CheckoutUrl }).SingleAsync(token);
            course.PaymentStatus = state.PaymentStatus; course.Status = state.Status; course.CheckoutUrl = state.CheckoutUrl;
            if (state.CheckoutUrl is null && state.PaymentStatus == "Pending")
            {
                var root = settings.FrontendBaseUrl.TrimEnd('/');
                var checkout = await payments.CreatePaymentLinkAsync(700_000_000L + course.Id, (int)course.Amount,
                    $"COURSE {course.Id}", root + "/my-courses", root + "/my-courses", token);
                Require(Uri.TryCreate(checkout.CheckoutUrl, UriKind.Absolute, out var url) && url.Scheme == "https", "Cổng thanh toán trả link không hợp lệ.");
                course.CheckoutUrl = checkout.CheckoutUrl;
            }
            await uow.SaveChangesAsync(token);
        }, ct);
        return new { course.Id, course.CheckoutUrl, course.PaymentStatus, course.Amount };
    });

    public async Task<ApiResponse<object>> ConfirmPaymentAsync(int courseId, PaymentCallbackResult callback, string? reference, CancellationToken ct)
    {
        // This service method is called only after signature verification in PaymentService; it has no public controller route.
        if (!callback.IsValidSignature) return ApiResponse<object>.Fail("Chữ ký thanh toán không hợp lệ.");
        return await Run(async () =>
        {
            await uow.ExecuteInTransactionAsync(async token =>
            {
                var c = await uow.StudyCourses.LockAsync(courseId, token);
                Require(c is not null && c.RetakeOfId is null, "Không tìm thấy đơn mua khóa.");
                Require(callback.OrderCode == 700_000_000L + courseId, "Mã thanh toán không khớp đơn mua.");
                Require(callback.Amount == c!.Amount, "Số tiền không khớp đơn mua.");
                if (c.PaymentStatus == "Paid" || !callback.IsSuccess) return;
                // A valid paid webhook may arrive late. The checkout timer must never discard received money.
                c.PaymentStatus = "Paid"; c.Status = "Ready"; c.PaymentReference = reference;
                await uow.SaveChangesAsync(token);
            }, ct);
            return new { Confirmed = true };
        });
    }

    public Task<ApiResponse<object>> ActivateAsync(int id, ActivateCourse r, CancellationToken ct) => Change(id, async c =>
    {
        Admin(); Require(c.Status == "Ready" && c.PaymentStatus == "Paid", "Khóa chưa thanh toán hoặc đã được mở.");
        Require(r.StartsAt.Kind == DateTimeKind.Utc, "Ngày bắt đầu phải có múi giờ UTC.");
        Require(await uow.UserAccounts.Query().AnyAsync(x => x.Id == r.MentorId && x.Role == Role.Mentor && !x.IsDeleted, ct), "Tài khoản Mentor không hợp lệ hoặc đã ngừng hoạt động.");
        c.MentorId = r.MentorId; c.StartsAt = r.StartsAt; c.EndsAt = r.StartsAt.AddMonths(c.DurationMonths); c.Status = "Active";
        Audit(c, "Activated", $"Mentor {r.MentorId}; lịch học và điều kiện theo phiên bản {c.PlanRevision}.");
        return new { c.Id };
    }, ct);

    public Task<ApiResponse<object>> ScheduleAsync(int id, ScheduleCourseSession r, CancellationToken ct) => Change(id, async c =>
    {
        Admin(); Active(c);
        await uow.StudyCourses.LockScheduleAsync(c.MentorId!.Value, c.LearnerId, ct);
        Require(r.Number >= 1 && r.Number <= c.TotalSessions && r.ScheduledAt.Kind == DateTimeKind.Utc
            && r.ScheduledAt >= c.StartsAt && r.ScheduledAt.AddMinutes(c.SessionMinutes) <= c.EndsAt, "Buổi học ngoài phạm vi cam kết.");
        Require(string.IsNullOrWhiteSpace(r.MeetingUrl) || (Uri.TryCreate(r.MeetingUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https"), "Link học phải là HTTPS.");
        var group = c.Sessions.Where(x => x.Number == r.Number).ToList();
        Require(!group.Any(x => x.Status is "Scheduled" or "Present"), "Buổi này còn lịch chờ học hoặc đã tham dự.");
        if (group.Count == 0)
        {
            // Cadence uses Vietnam time; make-up attempts share their original session number.
            var local = r.ScheduledAt.AddHours(7);
            var weekStart = local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7)).AddHours(-7);
            var weekEnd = weekStart.AddDays(7);
            Require(c.Sessions.Count(x => x.Attempt == 1 && x.Status != "CancelledByMentor"
                && x.ScheduledAt >= weekStart && x.ScheduledAt < weekEnd) < c.SessionsPerWeek,
                "Tuần này đã đủ số buổi gốc theo gói. Buổi học bù cần chọn lại số buổi gốc.");
        }
        var end = r.ScheduledAt.AddMinutes(c.SessionMinutes);
        var conflict = await uow.StudyCourses.Query().Where(x => x.Status == "Active" && (x.MentorId == c.MentorId || x.LearnerId == c.LearnerId))
            .AnyAsync(x => x.Sessions.Any(s => s.Status == "Scheduled" && s.ScheduledAt < end
                && s.ScheduledAt.AddMinutes(x.SessionMinutes) > r.ScheduledAt), ct);
        Require(!conflict, "Mentor hoặc học viên bị trùng lịch khóa học.");
        Require(!await uow.MentorBookings.Query().AnyAsync(b => !b.IsDeleted && b.UserSubscription.UserId == c.LearnerId
            && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted) && b.MentorAvailability != null
            && b.MentorAvailability.StartTime < end && b.MentorAvailability.EndTime > r.ScheduledAt, ct),
            "Học viên đã có lịch tư vấn trùng giờ này.");
        Require(!await uow.MentorAvailabilities.Query().AnyAsync(x => !x.IsDeleted && x.MentorId == c.MentorId
            && x.IsApproved && x.StartTime < end && x.EndTime > r.ScheduledAt, ct),
            "Mentor đã mở khung tư vấn trong giờ này. Hãy chọn giờ khác.");
        c.Sessions.Add(new() { Number = r.Number, Attempt = group.Count + 1, ScheduledAt = r.ScheduledAt, MeetingUrl = r.MeetingUrl });
        Audit(c, "Scheduled", $"Buổi {r.Number}, lần {group.Count + 1}: {r.ScheduledAt:O}.");
        return new { c.Id };
    }, ct);

    public Task<ApiResponse<object>> AttendanceAsync(int id, int sessionId, ConfirmCourseAttendance r, CancellationToken ct) => Change(id, c =>
    {
        if (Actor.Role != Role.Admin) Teacher(c); Active(c);
        var session = c.Sessions.SingleOrDefault(x => x.Id == sessionId);
        Require(session is not null && session.Status == "Scheduled", "Buổi đã được chốt hoặc không tồn tại.");
        Require(r.Status is "Present" or "Absent" or "ExcusedAbsent" or "CancelledByMentor", "Trạng thái điểm danh không hợp lệ.");
        Require(!string.IsNullOrWhiteSpace(r.Note) && r.Note.Length <= 1000, "Cần ghi lý do/xác nhận, tối đa 1000 ký tự.");
        Require(r.Status == "CancelledByMentor" || session!.ScheduledAt <= DateTime.UtcNow, "Chưa được điểm danh buổi trong tương lai.");
        session!.Status = r.Status; session.Note = r.Note.Trim(); session.ConfirmedById = Actor.Id; session.ConfirmedAt = DateTime.UtcNow;
        Audit(c, "Attendance", $"Buổi {session.Number}/{session.Attempt}: {r.Status}. {r.Note.Trim()}");
        return Task.FromResult<object>(new { session.Id });
    }, ct);

    public Task<ApiResponse<object>> AssignAsync(int id, AssignCourseHomework r, CancellationToken ct) => Change(id, c =>
    {
        Teacher(c); Active(c);
        Require(!string.IsNullOrWhiteSpace(r.Title) && r.Title.Length <= 200 && r.Instructions.Length <= 10000
            && r.DueAt.Kind == DateTimeKind.Utc && r.DueAt > DateTime.UtcNow && r.DueAt <= c.EndsAt, "Kiểm tra tên bài, hướng dẫn và hạn nộp trong khóa.");
        c.Homework.Add(new() { Title = r.Title.Trim(), Instructions = r.Instructions, Required = r.Required, DueAt = r.DueAt });
        Audit(c, "HomeworkAssigned", r.Title.Trim());
        return Task.FromResult<object>(new { c.Id });
    }, ct);

    public Task<ApiResponse<object>> SubmitHomeworkAsync(int id, int homeworkId, SubmitCourseHomework r, CancellationToken ct) => Change(id, c =>
    {
        Learner(c); Active(c);
        var h = c.Homework.SingleOrDefault(x => x.Id == homeworkId);
        Require(h is not null && h.Passed != true, "Bài đã được xác nhận đạt hoặc không tồn tại.");
        Require(!string.IsNullOrWhiteSpace(r.Content) && r.Content.Length <= 20000, "Bài nộp phải từ 1 đến 20000 ký tự.");
        h!.Submission = r.Content.Trim(); h.SubmittedAt = DateTime.UtcNow; h.Passed = null; h.ReviewedAt = null; h.Feedback = null;
        Audit(c, "HomeworkSubmitted", $"Bài {h.Id}, giờ nộp do server ghi nhận.");
        return Task.FromResult<object>(new { h.Id });
    }, ct);

    public Task<ApiResponse<object>> GradeAsync(int id, int homeworkId, GradeCourseHomework r, CancellationToken ct) => Change(id, c =>
    {
        Teacher(c); Active(c);
        var h = c.Homework.SingleOrDefault(x => x.Id == homeworkId);
        Require(h?.SubmittedAt is not null, "Học viên chưa nộp bài.");
        Require(!string.IsNullOrWhiteSpace(r.Feedback) && r.Feedback.Length <= 2000, "Cần nhận xét tối đa 2000 ký tự.");
        h!.Passed = r.Passed; h.ReviewedAt = DateTime.UtcNow; h.Feedback = r.Feedback.Trim();
        Audit(c, "HomeworkReviewed", $"Bài {h.Id}: {(r.Passed ? "Đạt" : "Chưa đạt")}.");
        return Task.FromResult<object>(new { h.Id });
    }, ct);

    public Task<ApiResponse<object>> CompleteAsync(int id, CancellationToken ct) => Change(id, c =>
    {
        Admin(); Active(c);
        Require(c.Sessions.Select(x => x.Number).Distinct().Count() == c.TotalSessions && !c.Sessions.Any(x => x.Status == "Scheduled"), "Cần chốt đủ các buổi cam kết trước khi hoàn thành khóa.");
        Require(!c.Homework.Any(x => x.DueAt > DateTime.UtcNow || (x.SubmittedAt.HasValue && !x.Passed.HasValue)), "BTVN còn hạn hoặc đang chờ Mentor xác nhận.");
        c.Status = "Completed"; c.CompletedAt = DateTime.UtcNow;
        Audit(c, "Completed", "Chốt BTVN và chuyên cần; không cho sửa sau khi khóa hoàn thành.");
        return Task.FromResult<object>(new { c.Id });
    }, ct);

    public Task<ApiResponse<object>> RequestRetakeAsync(int id, DateTime examDate, DateTime resultPublishedAt, IFormFile evidence, CancellationToken ct) => Change(id, async c =>
    {
        Learner(c);
        var eligibility = CourseRetakePolicy.Evaluate(c, await uow.StudyCourses.Query().AnyAsync(x => x.RetakeOfId == id, ct));
        Require(eligibility.CanClaim, string.Join(" ", eligibility.Reasons));
        Require(!c.RetakeRequests.Any(x => x.Status == "Pending"), "Đã có yêu cầu đang chờ duyệt.");
        Require(examDate.Kind == DateTimeKind.Utc && resultPublishedAt.Kind == DateTimeKind.Utc
            && examDate.Date >= c.StartsAt!.Value.Date && examDate <= resultPublishedAt && resultPublishedAt <= DateTime.UtcNow, "Kiểm tra ngày thi và ngày công bố kết quả JLPT.");
        Require(!c.RetakeClaimDays.HasValue || DateTime.UtcNow <= resultPublishedAt.AddDays(c.RetakeClaimDays.Value), "Đã quá hạn gửi yêu cầu theo chính sách khi mua.");
        Require(evidence is not null && evidence.Length is > 0 and <= 10 * 1024 * 1024
            && evidence.ContentType is "application/pdf" or "image/png" or "image/jpeg", "Minh chứng phải là PDF/PNG/JPG tối đa 10 MB.");
        await using var stream = evidence!.OpenReadStream();
        var file = await storage.UploadPrivateVerificationAsync(stream, evidence.ContentType, Actor.Id, ct);
        c.RetakeRequests.Add(new() { ExamDate = examDate, ResultPublishedAt = resultPublishedAt,
            EvidenceUrl = file.Url, EligibilitySnapshot = JsonSerializer.Serialize(eligibility) });
        Audit(c, "RetakeRequested", "Học viên khai báo trượt JLPT chính thức; chờ Admin kiểm tra danh tính, cấp độ và kết quả.");
        return new { c.Id };
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
            Require(r.OfficialFailureVerified, "Admin phải xác nhận minh chứng trượt JLPT, đúng học viên và cấp độ của khóa.");
            var eligibility = CourseRetakePolicy.Evaluate(c, await uow.StudyCourses.Query().AnyAsync(x => x.RetakeOfId == id, ct));
            Require(eligibility.CanClaim, string.Join(" ", eligibility.Reasons));
            var retake = new StudyCourse { LearnerId = c.LearnerId, PurchaseKey = Guid.NewGuid(), PlanId = c.PlanId,
                RetakeOfId = c.Id, Name = c.Name, CertificateLevelId = c.CertificateLevelId, PlanRevision = c.PlanRevision,
                DurationMonths = c.DurationMonths, SessionsPerWeek = c.SessionsPerWeek, SessionMinutes = c.SessionMinutes,
                TotalSessions = c.TotalSessions, RetakeClaimDays = c.RetakeClaimDays, MinimumHomeworkPercent = c.MinimumHomeworkPercent,
                MaximumAbsencePercent = c.MaximumAbsencePercent, Amount = 0, PaymentStatus = "Paid", Status = "Ready" };
            Audit(retake, "FreeRetakeCreated", $"Học lại một lần của khóa {c.Id}; không phát sinh quyền học lại tiếp.");
            await uow.StudyCourses.AddAsync(retake, ct); await uow.SaveChangesAsync(ct); newCourseId = retake.Id;
        }
        request!.Status = r.Approve ? "Approved" : "Rejected"; request.Response = r.Response.Trim();
        request.ReviewedById = Actor.Id; request.ReviewedAt = DateTime.UtcNow;
        Audit(c, "RetakeReviewed", $"Yêu cầu {requestId}: {request.Status}. {r.Response.Trim()}");
        return new { NewCourseId = newCourseId };
    }, ct);

    public async Task<PrivateVerificationReadResult?> EvidenceAsync(int id, int requestId, CancellationToken ct)
    {
        var c = await Details.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (c is null || (Actor.Role != Role.Admin && !(Actor.Role == Role.Learner && c.LearnerId == Actor.Id))) return null;
        var r = c.RetakeRequests.SingleOrDefault(x => x.Id == requestId);
        return r is null ? null : await storage.OpenPrivateVerificationAsync(r.EvidenceUrl, ct);
    }
}
