using Microsoft.AspNetCore.Http;
using RikiPath.Application.IClients;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public interface IStudyCourseService
{
    Task<ApiResponse<object>> ExamSchedulesAsync(CancellationToken ct);
    Task<ApiResponse<object>> ImportExamSchedulesAsync(CancellationToken ct);
    Task<ApiResponse<object>> ReviewExamScheduleAsync(int id, DateOnly date, string location, bool approve, string note, CancellationToken ct);
    Task<ApiResponse<object>> ChangeRequestsAsync(CancellationToken ct);
    Task<ApiResponse<object>> RequestChangeAsync(int id, RequestCourseChange r, CancellationToken ct);
    Task<ApiResponse<object>> ReviewChangeAsync(int id, int requestId, ReviewCourseChange r, CancellationToken ct);
    Task<ApiResponse<object>> CohortsAsync(CancellationToken ct);
    // Tìm lớp nhóm theo Mentor, cấp độ, cường độ, thứ và khung giờ (giờ Việt Nam).
    Task<ApiResponse<object>> CohortsAsync(CohortDiscoveryFilter? filter, CancellationToken ct);
    Task<ApiResponse<object>> SaveCohortAsync(int? id, SaveStudyCohort request, CancellationToken ct);
    // Admin xem trước xung đột lịch dạy của Mentor cho các khung giờ đã chọn (không ghi dữ liệu).
    Task<ApiResponse<object>> CohortSchedulePreviewAsync(CohortSchedulePreviewRequest request, CancellationToken ct);
    Task<ApiResponse<object>> CatalogAsync(CancellationToken ct);
    Task<ApiResponse<object>> AdminCatalogAsync(CancellationToken ct);
    Task<ApiResponse<object>> MentorsAsync(CancellationToken ct);
    Task<ApiResponse<object>> SaveCertificateAsync(int? id, SaveCourseCertificate request, CancellationToken ct);
    Task<ApiResponse<object>> SaveLevelAsync(int? id, SaveCourseLevel request, CancellationToken ct);
    Task<ApiResponse<object>> PlansAsync(CancellationToken ct);
    Task<ApiResponse<object>> SavePlanAsync(int? id, SaveCoursePlan request, CancellationToken ct);
    Task<ApiResponse<object>> CoursesAsync(CancellationToken ct);
    Task<ApiResponse<object>> DetailAsync(int id, CancellationToken ct);
    Task<ApiResponse<object>> PurchaseAsync(PurchaseCourse request, CancellationToken ct);
    Task<ApiResponse<object>> EnrollCohortAsync(int cohortId, EnrollCohortRequest request, CancellationToken ct);
    Task<ApiResponse<object>> CancelPendingEnrollmentAsync(int id, CancellationToken ct);
    Task<ApiResponse<object>> CancelCohortEnrollmentAsync(int id, CancelCohortEnrollmentRequest? request, CancellationToken ct);
    Task<ApiResponse<object>> ConfirmPaymentAsync(int courseId, PaymentCallbackResult callback, string? reference, CancellationToken ct);
    Task<ApiResponse<object>> ReconcileCoursePaymentAsync(int courseId, CancellationToken ct);
    Task<ApiResponse<object>> AttendanceAsync(int id, int sessionId, ConfirmCourseAttendance request, CancellationToken ct);
    Task<ApiResponse<object>> CompleteAsync(int id, CancellationToken ct);
    Task<ApiResponse<object>> RequestRetakeAsync(int id, DateTime examDate, DateTime resultPublishedAt, IFormFile? evidence, CancellationToken ct, string? reason = null);
    Task<ApiResponse<object>> ReviewRetakeAsync(int id, int requestId, ReviewCourseRetake request, CancellationToken ct);
    Task<ApiResponse<object>> SaveRetakeSupportPlanAsync(int id, int requestId, SaveRetakeSupportPlan request, CancellationToken ct);
    Task<PrivateVerificationReadResult?> EvidenceAsync(int id, int requestId, CancellationToken ct);
    // Chuyển lớp có kiểm soát (Học viên yêu cầu, Admin duyệt).
    Task<ApiResponse<object>> RequestTransferAsync(int courseId, int? targetCohortId, string? reason, Guid idempotencyKey, IFormFile? evidence, CancellationToken ct);
    Task<ApiResponse<object>> TransferRequestsAsync(string? status, CancellationToken ct);
    Task<ApiResponse<object>> TransferRequestDetailAsync(int requestId, CancellationToken ct);
    Task<ApiResponse<object>> TransferEligibilityAsync(int requestId, int targetCohortId, CancellationToken ct);
    Task<ApiResponse<object>> ApproveTransferAsync(int requestId, ApproveCohortTransfer request, CancellationToken ct);
    Task<ApiResponse<object>> RejectTransferAsync(int requestId, RejectCohortTransfer request, CancellationToken ct);
    Task<ApiResponse<object>> WithdrawTransferAsync(int requestId, CancellationToken ct);
    Task<PrivateVerificationReadResult?> TransferEvidenceAsync(int requestId, CancellationToken ct);
    // Admin thay cố vấn chính cho phần còn lại của lớp.
    Task<ApiResponse<object>> PreviewMentorReplacementAsync(int cohortId, int mentorId, int? fromSessionNumber, CancellationToken ct);
    Task<ApiResponse<object>> ReplaceCohortMentorAsync(int cohortId, ReplaceCohortMentorRequest request, CancellationToken ct);
    Task<ApiResponse<object>> MentorAssignmentsAsync(int cohortId, CancellationToken ct);
    // Dời toàn bộ lịch / hủy lớp chưa có học viên (Mentor chính hoặc Admin yêu cầu, Admin duyệt từng lần).
    Task<ApiResponse<object>> CreateCohortScheduleRequestAsync(int cohortId, CreateCohortScheduleRequest request, CancellationToken ct);
    Task<ApiResponse<object>> CohortScheduleRequestsAsync(int cohortId, CancellationToken ct);
    Task<ApiResponse<object>> CohortScheduleRequestQueueAsync(string? status, CancellationToken ct);
    Task<ApiResponse<object>> CohortShiftPreviewAsync(int cohortId, int weeks, CancellationToken ct);
    Task<ApiResponse<object>> ApproveCohortScheduleRequestAsync(int requestId, ApproveCohortScheduleRequest request, CancellationToken ct);
    Task<ApiResponse<object>> RejectCohortScheduleRequestAsync(int requestId, RejectCohortScheduleRequest request, CancellationToken ct);
    Task<ApiResponse<object>> WithdrawCohortScheduleRequestAsync(int requestId, CancellationToken ct);
    // Đánh giá cố vấn, khiếu nại và báo cáo tổng kết hoàn thành khóa/lớp nhóm.
    Task<ApiResponse<object>> CreateCohortMentorReviewAsync(int courseId, CreateCohortMentorReviewRequest request, CancellationToken ct);
    Task<ApiResponse<object>> UpdateCohortMentorReviewAsync(int courseId, int reviewId, UpdateCohortMentorReviewRequest request, CancellationToken ct);
    Task<ApiResponse<object>> CohortMentorReviewsAsync(int courseId, CancellationToken ct);
    Task<ApiResponse<object>> MentorCohortReviewSummaryAsync(int mentorId, CancellationToken ct);
    Task<ApiResponse<object>> CreateCohortComplaintAsync(int courseId, string? category, string? message, int? mentorId, bool refundReviewRequested, Guid idempotencyKey, IFormFile? evidence, CancellationToken ct);
    Task<ApiResponse<object>> CohortComplaintsAsync(string? status, CancellationToken ct);
    Task<ApiResponse<object>> CohortComplaintDetailAsync(int complaintId, CancellationToken ct);
    Task<ApiResponse<object>> RespondCohortComplaintAsync(int complaintId, RespondCohortComplaintRequest request, CancellationToken ct);
    Task<ApiResponse<object>> DecideCohortComplaintAsync(int complaintId, DecideCohortComplaintRequest request, CancellationToken ct);
    Task<PrivateVerificationReadResult?> CohortComplaintEvidenceAsync(int complaintId, CancellationToken ct);
    Task<ApiResponse<object>> CourseCompletionSummaryAsync(int courseId, CancellationToken ct);
}
