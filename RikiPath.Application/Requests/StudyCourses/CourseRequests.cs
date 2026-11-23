namespace RikiPath.Application.Requests.StudyCourses;

public sealed record SaveCoursePlan(string Name, int CertificateLevelId, int DurationMonths, int SessionsPerWeek,
    int? SessionMinutes, int? TotalSessions, int? RetakeClaimDays, decimal Price, bool IsPublished, int Revision = 1,
    string SessionType = "Individual", int Capacity = 1, DateTime? TargetExamDate = null);
public sealed record PurchaseCourse(int PlanId, Guid IdempotencyKey, int? CohortId = null, string? EntitlementReference = null);
public sealed record EnrollCohortRequest(Guid IdempotencyKey, string? EntitlementReference = null);
public sealed record CancelCohortEnrollmentRequest(string? Reason = null);
public sealed record ActivateCourse(int MentorId, DateTime StartsAt);
public sealed record ScheduleCourseSession(int Number, DateTime ScheduledAt, string? MeetingUrl);
public sealed record ConfirmCourseAttendance(string Status, string Note);
public sealed record AssignCourseHomework(string Title, string Instructions, bool Required, DateTime DueAt);
public sealed record SubmitCourseHomework(string Content);
public sealed record GradeCourseHomework(bool Passed, string Feedback);
public sealed record ReviewCourseRetake(bool Approve, bool OfficialFailureVerified, string Response,
    bool OnTimeHomeworkEvidenceVerified = false, int? CohortId = null);
public sealed record SaveRetakeSupportPlan(string SupportPlan);
public sealed record GrantCohortEntitlementRequest(int LearnerId, string SourceType = "AdminManualGrant",
    string? EntitlementReference = null, string? Note = null, DateTime? ExpiresAtUtc = null);
public sealed record RevokeCohortEntitlementRequest(string Reason);
public sealed record SaveStudyCohort(int PlanId, string Name, int MentorId, int Capacity,
    List<int> AvailabilityIds, bool Publish, int Revision = 1, int? SourceProposalId = null);
// Xem trước xung đột lịch dạy khi Admin chọn khung giờ cho lớp (chỉ tham khảo; lưu lớp vẫn kiểm tra lại).
public sealed record CohortSchedulePreviewRequest(int MentorId, List<int> AvailabilityIds, int? CohortId = null);
// Bộ lọc tìm lớp nhóm: Mentor, cấp độ JLPT, cường độ (Light/Standard/Advanced), thứ (0=Chủ nhật..6, giờ Việt Nam)
// và khung giờ bắt đầu buổi học (giờ Việt Nam).
public sealed record CohortDiscoveryFilter(int? MentorId = null, string? Level = null, string? Intensity = null,
    int? Weekday = null, TimeOnly? FromTime = null, TimeOnly? ToTime = null);
public sealed record BookCourseSlot(int AvailabilityId);
public sealed record SaveCourseCertificate(
    string Name,
    string? Description,
    int? SortOrder,
    bool IsActive,
    bool AllowShift = false,
    int? ExpectedRevision = null,
    string? ExpectedOrderToken = null);

public sealed record SaveCourseLevel(
    int CertificateTypeId,
    string Code,
    string? Description,
    int? SortOrder,
    bool IsActive,
    bool AllowShift = false,
    int? ExpectedRevision = null,
    string? ExpectedOrderToken = null);

public sealed record ReorderCourseCatalogRequest(
    int TargetSortOrder,
    int? ExpectedRevision = null,
    string? ExpectedOrderToken = null);

public sealed record ReviewJlptExam(DateOnly ExamDate, string Location, bool Approve, string Note);

public sealed record RequestCourseChange(int SessionId, string Kind, string Reason);
public sealed record ApproveCohortTransfer(int? TargetCohortId = null, string? Note = null, int? ExpectedRevision = null);
public sealed record RejectCohortTransfer(string Reason, int? ExpectedRevision = null);
/// <summary>Mentor chính/Admin yêu cầu dời toàn bộ lớp trống (Kind = "ShiftWeeks", ShiftWeeks 1..8) hoặc hủy lớp trống (Kind = "CancelEmptyCohort").</summary>
public sealed record CreateCohortScheduleRequest(string Kind, int? ShiftWeeks, string Reason, Guid IdempotencyKey);
public sealed record ApproveCohortScheduleRequest(string? Note = null, int? ExpectedRevision = null);
public sealed record RejectCohortScheduleRequest(string Reason, int? ExpectedRevision = null);
public sealed record ReplaceCohortMentorRequest(int MentorId, string Reason, Guid IdempotencyKey, int ExpectedCohortRevision, int? FromSessionNumber = null);
public sealed record CreateCohortMentorReviewRequest(int MentorId, int Rating, string? Comment = null);
public sealed record UpdateCohortMentorReviewRequest(int Rating, string? Comment = null);
public sealed record RespondCohortComplaintRequest(string Response);
public sealed record DecideCohortComplaintRequest(string Status, string Decision, int? ExpectedRevision = null);
public sealed record ReviewCourseChange(bool Approve, string Response, int? NewAvailabilityId = null, bool RestoreSessionCredit = false, bool EntireClassConfirmed = false);
