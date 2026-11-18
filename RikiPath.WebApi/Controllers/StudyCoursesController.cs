using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController, Authorize(Roles = "Admin,Learner,Mentor"), Route("api/study-courses")]
public sealed class StudyCoursesController(
    IStudyCourseService service,
    ICohortEnrollmentEntitlementService entitlements) : ControllerBase
{
    private IActionResult Result(ApiResponse<object> r) => StatusCode(r.StatusCode, r);
    [HttpGet("exam-schedules")]
    public async Task<IActionResult> ExamSchedules(CancellationToken ct) => Result(await service.ExamSchedulesAsync(ct));
    [HttpPost("exam-schedules/import"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ImportExams(CancellationToken ct) => Result(await service.ImportExamSchedulesAsync(ct));
    [HttpPost("exam-schedules/{id:int}/review"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewExam(int id, ReviewJlptExam r, CancellationToken ct) => Result(await service.ReviewExamScheduleAsync(id, r.ExamDate, r.Location, r.Approve, r.Note, ct));
    [HttpGet("change-requests")]
    public async Task<IActionResult> ChangeRequests(CancellationToken ct) => Result(await service.ChangeRequestsAsync(ct));
    [HttpPost("{id:int}/change-requests")]
    public async Task<IActionResult> RequestChange(int id, RequestCourseChange r, CancellationToken ct) => Result(await service.RequestChangeAsync(id, r, ct));
    [HttpPost("{id:int}/change-requests/{requestId:int}/review"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewChange(int id, int requestId, ReviewCourseChange r, CancellationToken ct) => Result(await service.ReviewChangeAsync(id, requestId, r, ct));
    /// <summary>Danh sách lớp nhóm; lọc tùy chọn theo Mentor, cấp độ, cường độ (Light/Standard/Advanced),
    /// thứ (0=Chủ nhật..6) và giờ bắt đầu buổi học "HH:mm" (giờ Việt Nam).</summary>
    [HttpGet("cohorts")]
    public async Task<IActionResult> Cohorts([FromQuery] int? mentorId, [FromQuery] string? level, [FromQuery] string? intensity,
        [FromQuery] int? weekday, [FromQuery] string? fromTime, [FromQuery] string? toTime, CancellationToken ct)
    {
        if (mentorId is null && string.IsNullOrWhiteSpace(level) && string.IsNullOrWhiteSpace(intensity) && weekday is null
            && string.IsNullOrWhiteSpace(fromTime) && string.IsNullOrWhiteSpace(toTime))
            return Result(await service.CohortsAsync(ct));
        if (!TryParseLocalTime(fromTime, out var from) || !TryParseLocalTime(toTime, out var to))
            return Result(ApiResponse<object>.Fail("Giờ lọc phải theo định dạng HH:mm.", System.Net.HttpStatusCode.BadRequest));
        return Result(await service.CohortsAsync(new CohortDiscoveryFilter(mentorId, level, intensity, weekday, from, to), ct));
    }

    private static bool TryParseLocalTime(string? value, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!TimeOnly.TryParseExact(value.Trim(), ["HH:mm", "H:mm"], System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed)) return false;
        time = parsed;
        return true;
    }
    [HttpGet("cohorts/{cohortId:int}/entitlements"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CohortEntitlements(int cohortId, CancellationToken ct) => Result(await entitlements.ListForCohortAsync(cohortId, ct));
    [HttpPost("cohorts/{cohortId:int}/entitlements"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> GrantCohortEntitlement(int cohortId, GrantCohortEntitlementRequest r, CancellationToken ct) => Result(await entitlements.GrantAsync(cohortId, r, ct));
    [HttpPost("entitlements/{entitlementId:int}/revoke"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeCohortEntitlement(int entitlementId, RevokeCohortEntitlementRequest r, CancellationToken ct) => Result(await entitlements.RevokeAsync(entitlementId, r, ct));
    [HttpGet("my-entitlements"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> MyEntitlements(CancellationToken ct) => Result(await entitlements.MyEntitlementsAsync(ct));
    [HttpPost("cohorts"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCohort(SaveStudyCohort r, CancellationToken ct) => Result(await service.SaveCohortAsync(null, r, ct));
    /// <summary>
    /// Admin xem trước xung đột lịch dạy của Mentor (trùng giờ, thời gian nghỉ, giới hạn ngày/tuần) cho các khung giờ đã chọn.
    /// Chỉ mang tính tham khảo; khi lưu/công bố lớp hệ thống vẫn kiểm tra lại trong giao dịch.
    /// </summary>
    [HttpPost("cohorts/schedule-preview"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CohortSchedulePreview(CohortSchedulePreviewRequest r, CancellationToken ct)
        => Result(await service.CohortSchedulePreviewAsync(r, ct));
    [HttpPut("cohorts/{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCohort(int id, SaveStudyCohort r, CancellationToken ct) => Result(await service.SaveCohortAsync(id, r, ct));
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken ct) => Result(await service.CatalogAsync(ct));
    [HttpGet("admin-catalog"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminCatalog(CancellationToken ct) => Result(await service.AdminCatalogAsync(ct));
    [HttpGet("mentors"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Mentors(CancellationToken ct) => Result(await service.MentorsAsync(ct));
    [HttpPost("certificates"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCertificate(SaveCourseCertificate r, CancellationToken ct) => Result(await service.SaveCertificateAsync(null, r, ct));
    [HttpPut("certificates/{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveCertificate(int id, SaveCourseCertificate r, CancellationToken ct) => Result(await service.SaveCertificateAsync(id, r, ct));
    [HttpPost("levels"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateLevel(SaveCourseLevel r, CancellationToken ct) => Result(await service.SaveLevelAsync(null, r, ct));
    [HttpPut("levels/{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveLevel(int id, SaveCourseLevel r, CancellationToken ct) => Result(await service.SaveLevelAsync(id, r, ct));
    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct) => Result(await service.PlansAsync(ct));
    [HttpPost("plans"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(SaveCoursePlan r, CancellationToken ct) => Result(await service.SavePlanAsync(null, r, ct));
    [HttpPut("plans/{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(int id, SaveCoursePlan r, CancellationToken ct) => Result(await service.SavePlanAsync(id, r, ct));
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Result(await service.CoursesAsync(ct));
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct) => Result(await service.DetailAsync(id, ct));
    [HttpPost("purchase"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> Purchase(PurchaseCourse r, CancellationToken ct) => Result(await service.PurchaseAsync(r, ct));
    [HttpPost("cohorts/{cohortId:int}/enroll"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> EnrollCohort(int cohortId, EnrollCohortRequest r, CancellationToken ct)
        => Result(await service.EnrollCohortAsync(cohortId, r, ct));
    [HttpPost("{id:int}/cancel-pending-enrollment"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> CancelPendingEnrollment(int id, CancellationToken ct)
        => Result(await service.CancelPendingEnrollmentAsync(id, ct));
    /// <summary>Đối soát trạng thái thanh toán đơn khóa học với PayOS (truy vấn server-to-server; redirect phía client không phải bằng chứng). Học viên: đơn của mình; Admin: mọi đơn.</summary>
    [HttpPost("{id:int}/payment/reconcile"), Authorize(Roles = "Learner,Admin")]
    public async Task<IActionResult> ReconcilePayment(int id, CancellationToken ct)
        => Result(await service.ReconcileCoursePaymentAsync(id, ct));
    [HttpPost("{id:int}/cancel-enrollment"), Authorize(Roles = "Learner,Admin")]
    public async Task<IActionResult> CancelCohortEnrollment(int id, CancelCohortEnrollmentRequest? r, CancellationToken ct)
        => Result(await service.CancelCohortEnrollmentAsync(id, r, ct));
    [HttpPost("{id:int}/sessions/{sessionId:int}/attendance"), Authorize(Roles = "Admin,Mentor")]
    public async Task<IActionResult> Attendance(int id, int sessionId, ConfirmCourseAttendance r, CancellationToken ct) => Result(await service.AttendanceAsync(id, sessionId, r, ct));
    [HttpPost("{id:int}/complete"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct) => Result(await service.CompleteAsync(id, ct));
    [HttpPost("{id:int}/retake"), Authorize(Roles = "Learner"), RequestSizeLimit(11000000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RequestRetake(int id, [FromForm] DateTime examDate,
        [FromForm] DateTime resultPublishedAt, IFormFile? evidence, CancellationToken ct,
        [FromForm] string? reason = null)
        => Result(await service.RequestRetakeAsync(id, examDate, resultPublishedAt, evidence, ct, reason));
    [HttpPost("{id:int}/retake/{requestId:int}/review"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(int id, int requestId, ReviewCourseRetake r, CancellationToken ct)
        => Result(await service.ReviewRetakeAsync(id, requestId, r, ct));
    [HttpPost("{id:int}/retake/{requestId:int}/support-plan"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> SaveRetakeSupportPlan(int id, int requestId, SaveRetakeSupportPlan r, CancellationToken ct)
        => Result(await service.SaveRetakeSupportPlanAsync(id, requestId, r, ct));
    /// <summary>Học viên gửi yêu cầu chuyển lớp cho ghi danh lớp nhóm của mình (Admin duyệt; không tự đổi lớp).</summary>
    [HttpPost("{id:int}/transfer-requests"), Authorize(Roles = "Learner"), RequestSizeLimit(11000000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RequestTransfer(int id, [FromForm] Guid idempotencyKey, [FromForm] string? reason,
        [FromForm] int? targetCohortId, IFormFile? evidence, CancellationToken ct)
        => Result(await service.RequestTransferAsync(id, targetCohortId, reason, idempotencyKey, evidence, ct));
    /// <summary>Danh sách yêu cầu chuyển lớp (Admin: toàn bộ; Học viên: của mình).</summary>
    [HttpGet("transfer-requests"), Authorize(Roles = "Admin,Learner")]
    public async Task<IActionResult> TransferRequests([FromQuery] string? status, CancellationToken ct) => Result(await service.TransferRequestsAsync(status, ct));
    /// <summary>Chi tiết yêu cầu chuyển lớp kèm lịch sử, tiến độ học và lớp đích phù hợp (Admin).</summary>
    [HttpGet("transfer-requests/{requestId:int}"), Authorize(Roles = "Admin,Learner")]
    public async Task<IActionResult> TransferRequestDetail(int requestId, CancellationToken ct) => Result(await service.TransferRequestDetailAsync(requestId, ct));
    /// <summary>Xem trước điều kiện chuyển sang một lớp đích (sức chứa, lịch, cấp độ, chính sách).</summary>
    [HttpGet("transfer-requests/{requestId:int}/eligibility"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> TransferEligibility(int requestId, [FromQuery] int targetCohortId, CancellationToken ct)
        => Result(await service.TransferEligibilityAsync(requestId, targetCohortId, ct));
    /// <summary>Admin duyệt chuyển lớp; kiểm tra lại điều kiện và cập nhật ghi danh trong một giao dịch.</summary>
    [HttpPost("transfer-requests/{requestId:int}/approve"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveTransfer(int requestId, ApproveCohortTransfer r, CancellationToken ct) => Result(await service.ApproveTransferAsync(requestId, r, ct));
    /// <summary>Admin từ chối yêu cầu chuyển lớp (bắt buộc nhập lý do).</summary>
    [HttpPost("transfer-requests/{requestId:int}/reject"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectTransfer(int requestId, RejectCohortTransfer r, CancellationToken ct) => Result(await service.RejectTransferAsync(requestId, r, ct));
    /// <summary>Học viên rút yêu cầu chuyển lớp đang chờ.</summary>
    [HttpPost("transfer-requests/{requestId:int}/withdraw"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> WithdrawTransfer(int requestId, CancellationToken ct) => Result(await service.WithdrawTransferAsync(requestId, ct));
    /// <summary>Tải minh chứng riêng tư của yêu cầu chuyển lớp.</summary>
    [HttpGet("transfer-requests/{requestId:int}/evidence"), Authorize(Roles = "Admin,Learner")]
    public async Task<IActionResult> TransferEvidence(int requestId, CancellationToken ct)
    {
        var file = await service.TransferEvidenceAsync(requestId, ct);
        Response.Headers.CacheControl = "no-store";
        return file is null ? NotFound() : File(file.Content, file.ContentType);
    }
    /// <summary>Xem trước tác động khi thay cố vấn chính cho các buổi còn lại của lớp.</summary>
    [HttpGet("cohorts/{cohortId:int}/mentor-replacement/preview"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> PreviewMentorReplacement(int cohortId, [FromQuery] int mentorId, [FromQuery] int? fromSessionNumber, CancellationToken ct)
        => Result(await service.PreviewMentorReplacementAsync(cohortId, mentorId, fromSessionNumber, ct));
    /// <summary>Admin xác nhận thay cố vấn chính; buổi đã diễn ra giữ cố vấn thực dạy.</summary>
    [HttpPost("cohorts/{cohortId:int}/mentor-replacement"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReplaceCohortMentor(int cohortId, ReplaceCohortMentorRequest r, CancellationToken ct)
        => Result(await service.ReplaceCohortMentorAsync(cohortId, r, ct));
    /// <summary>Lịch sử phân công cố vấn chính của lớp.</summary>
    [HttpGet("cohorts/{cohortId:int}/mentor-assignments"), Authorize(Roles = "Admin,Mentor")]
    public async Task<IActionResult> MentorAssignments(int cohortId, CancellationToken ct) => Result(await service.MentorAssignmentsAsync(cohortId, ct));
    /// <summary>
    /// Mentor chính của lớp (hoặc Admin) gửi yêu cầu dời toàn bộ lịch lớp chưa có học viên thêm N tuần (giữ nguyên thứ/giờ và số buổi)
    /// hoặc hủy lớp trống. Gửi lại cùng mã chống trùng trả về đúng yêu cầu cũ; mỗi lớp chỉ có một yêu cầu đang chờ.
    /// </summary>
    [HttpPost("cohorts/{cohortId:int}/schedule-requests"), Authorize(Roles = "Admin,Mentor")]
    public async Task<IActionResult> CreateCohortScheduleRequest(int cohortId, CreateCohortScheduleRequest r, CancellationToken ct)
        => Result(await service.CreateCohortScheduleRequestAsync(cohortId, r, ct));
    /// <summary>Lịch sử yêu cầu dời/hủy của một lớp kèm xem trước hiện tại (Admin hoặc Mentor chính của lớp).</summary>
    [HttpGet("cohorts/{cohortId:int}/schedule-requests"), Authorize(Roles = "Admin,Mentor")]
    public async Task<IActionResult> CohortScheduleRequests(int cohortId, CancellationToken ct)
        => Result(await service.CohortScheduleRequestsAsync(cohortId, ct));
    /// <summary>Hàng đợi yêu cầu dời/hủy lớp cho Admin, lọc theo trạng thái (Pending/Approved/Rejected/Withdrawn).</summary>
    [HttpGet("cohort-schedule-requests"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CohortScheduleRequestQueue([FromQuery] string? status, CancellationToken ct)
        => Result(await service.CohortScheduleRequestQueueAsync(status, ct));
    /// <summary>
    /// Xem trước việc dời toàn bộ lớp thêm N tuần: điều kiện (mã lỗi), giao dịch chặn, giờ mới của từng buổi,
    /// báo cáo trùng lịch dạy chuẩn của Mentor và hạn đăng ký mới. Không ghi dữ liệu.
    /// </summary>
    [HttpGet("cohorts/{cohortId:int}/shift-preview"), Authorize(Roles = "Admin,Mentor")]
    public async Task<IActionResult> CohortShiftPreview(int cohortId, [FromQuery] int weeks = 1, CancellationToken ct = default)
        => Result(await service.CohortShiftPreviewAsync(cohortId, weeks, ct));
    /// <summary>Admin duyệt yêu cầu dời/hủy lớp; kiểm tra lại toàn bộ điều kiện và trùng lịch trong một giao dịch.</summary>
    [HttpPost("cohort-schedule-requests/{requestId:int}/approve"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveCohortScheduleRequest(int requestId, ApproveCohortScheduleRequest r, CancellationToken ct)
        => Result(await service.ApproveCohortScheduleRequestAsync(requestId, r, ct));
    /// <summary>Admin từ chối yêu cầu dời/hủy lớp (bắt buộc nhập lý do).</summary>
    [HttpPost("cohort-schedule-requests/{requestId:int}/reject"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectCohortScheduleRequest(int requestId, RejectCohortScheduleRequest r, CancellationToken ct)
        => Result(await service.RejectCohortScheduleRequestAsync(requestId, r, ct));
    /// <summary>Người gửi rút yêu cầu dời/hủy lớp đang chờ.</summary>
    [HttpPost("cohort-schedule-requests/{requestId:int}/withdraw"), Authorize(Roles = "Admin,Mentor")]
    public async Task<IActionResult> WithdrawCohortScheduleRequest(int requestId, CancellationToken ct)
        => Result(await service.WithdrawCohortScheduleRequestAsync(requestId, ct));
    [HttpGet("{id:int}/retake/{requestId:int}/evidence"), Authorize(Roles = "Admin,Learner")]
    public async Task<IActionResult> Evidence(int id, int requestId, CancellationToken ct)
    {
        var file = await service.EvidenceAsync(id, requestId, ct);
        Response.Headers.CacheControl = "no-store";
        return file is null ? NotFound() : File(file.Content, file.ContentType);
    }
    /// <summary>Học viên đánh giá cố vấn của khóa học sau khi đã có ít nhất một buổi học được ghi nhận có mặt.</summary>
    [HttpPost("{courseId:int}/mentor-reviews"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> CreateCohortMentorReview(int courseId, CreateCohortMentorReviewRequest r, CancellationToken ct)
        => Result(await service.CreateCohortMentorReviewAsync(courseId, r, ct));
    /// <summary>Học viên cập nhật đánh giá cố vấn của chính mình trong khóa học.</summary>
    [HttpPut("{courseId:int}/mentor-reviews/{reviewId:int}"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> UpdateCohortMentorReview(int courseId, int reviewId, UpdateCohortMentorReviewRequest r, CancellationToken ct)
        => Result(await service.UpdateCohortMentorReviewAsync(courseId, reviewId, r, ct));
    /// <summary>Danh sách đánh giá cố vấn của một khóa học (Học viên sở hữu, Admin hoặc Cố vấn tham gia giảng dạy).</summary>
    [HttpGet("{courseId:int}/mentor-reviews"), Authorize(Roles = "Admin,Learner,Mentor")]
    public async Task<IActionResult> CohortMentorReviews(int courseId, CancellationToken ct)
        => Result(await service.CohortMentorReviewsAsync(courseId, ct));
    /// <summary>Tổng hợp điểm trung bình, số lượt đánh giá và tối đa 20 nhận xét công khai gần nhất của cố vấn (ẩn danh tính học viên).</summary>
    [HttpGet("mentors/{mentorId:int}/cohort-review-summary")]
    public async Task<IActionResult> MentorCohortReviewSummary(int mentorId, CancellationToken ct)
        => Result(await service.MentorCohortReviewSummaryAsync(mentorId, ct));
    /// <summary>Học viên gửi khiếu nại kèm minh chứng tùy chọn cho khóa học hoặc đơn thanh toán cần xem xét.</summary>
    [HttpPost("{courseId:int}/complaints"), Authorize(Roles = "Learner"), RequestSizeLimit(11000000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateCohortComplaint(
        int courseId,
        [FromForm] string category,
        [FromForm] string message,
        [FromForm] Guid idempotencyKey,
        [FromForm] int? mentorId,
        [FromForm] bool refundReviewRequested = false,
        IFormFile? evidence = null,
        CancellationToken ct = default)
        => Result(await service.CreateCohortComplaintAsync(courseId, category, message, mentorId, refundReviewRequested, idempotencyKey, evidence, ct));
    /// <summary>Danh sách khiếu nại khóa học theo phạm vi người dùng (Admin: toàn bộ; Học viên: của mình; Cố vấn: nhắm tới mình, ẩn thông tin thanh toán).</summary>
    [HttpGet("complaints"), Authorize(Roles = "Admin,Learner,Mentor")]
    public async Task<IActionResult> CohortComplaints([FromQuery] string? status, CancellationToken ct)
        => Result(await service.CohortComplaintsAsync(status, ct));
    /// <summary>Chi tiết khiếu nại khóa học theo phạm vi người dùng (trả về 404 nếu ngoài phạm vi).</summary>
    [HttpGet("complaints/{id:int}"), Authorize(Roles = "Admin,Learner,Mentor")]
    public async Task<IActionResult> CohortComplaintDetail(int id, CancellationToken ct)
        => Result(await service.CohortComplaintDetailAsync(id, ct));
    /// <summary>Cố vấn được nêu trong khiếu nại gửi phản hồi (không áp dụng cho khiếu nại thanh toán/ghi danh).</summary>
    [HttpPost("complaints/{id:int}/mentor-response"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> RespondCohortComplaint(int id, RespondCohortComplaintRequest r, CancellationToken ct)
        => Result(await service.RespondCohortComplaintAsync(id, r, ct));
    /// <summary>Admin ra quyết định giải quyết hoặc từ chối khiếu nại (bắt buộc nhập nội dung quyết định).</summary>
    [HttpPost("complaints/{id:int}/decision"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> DecideCohortComplaint(int id, DecideCohortComplaintRequest r, CancellationToken ct)
        => Result(await service.DecideCohortComplaintAsync(id, r, ct));
    /// <summary>Tải tệp minh chứng riêng tư của khiếu nại khóa học (Admin hoặc Học viên sở hữu; không lưu cache).</summary>
    [HttpGet("complaints/{id:int}/evidence"), Authorize(Roles = "Admin,Learner")]
    public async Task<IActionResult> CohortComplaintEvidence(int id, CancellationToken ct)
    {
        var file = await service.CohortComplaintEvidenceAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        return file is null ? NotFound() : File(file.Content, file.ContentType);
    }
    /// <summary>Báo cáo tổng kết quá trình học và hoàn thành khóa học nội bộ (không phải chứng chỉ JLPT chính thức).</summary>
    [HttpGet("{courseId:int}/completion-summary"), Authorize(Roles = "Admin,Learner,Mentor")]
    public async Task<IActionResult> CourseCompletionSummary(int courseId, CancellationToken ct)
        => Result(await service.CourseCompletionSummaryAsync(courseId, ct));
}
