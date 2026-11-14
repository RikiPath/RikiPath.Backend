using Microsoft.AspNetCore.Http;
using RikiPath.Application.IClients;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public interface IStudyCourseService
{
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
    Task<ApiResponse<object>> ConfirmPaymentAsync(int courseId, PaymentCallbackResult callback, string? reference, CancellationToken ct);
    Task<ApiResponse<object>> ActivateAsync(int id, ActivateCourse request, CancellationToken ct);
    Task<ApiResponse<object>> ScheduleAsync(int id, ScheduleCourseSession request, CancellationToken ct);
    Task<ApiResponse<object>> AttendanceAsync(int id, int sessionId, ConfirmCourseAttendance request, CancellationToken ct);
    Task<ApiResponse<object>> AssignAsync(int id, AssignCourseHomework request, CancellationToken ct);
    Task<ApiResponse<object>> SubmitHomeworkAsync(int id, int homeworkId, SubmitCourseHomework request, CancellationToken ct);
    Task<ApiResponse<object>> GradeAsync(int id, int homeworkId, GradeCourseHomework request, CancellationToken ct);
    Task<ApiResponse<object>> CompleteAsync(int id, CancellationToken ct);
    Task<ApiResponse<object>> RequestRetakeAsync(int id, DateTime examDate, DateTime resultPublishedAt, IFormFile evidence, CancellationToken ct);
    Task<ApiResponse<object>> ReviewRetakeAsync(int id, int requestId, ReviewCourseRetake request, CancellationToken ct);
    Task<PrivateVerificationReadResult?> EvidenceAsync(int id, int requestId, CancellationToken ct);
}
