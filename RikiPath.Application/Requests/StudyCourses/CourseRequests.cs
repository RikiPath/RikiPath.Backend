namespace RikiPath.Application.Requests.StudyCourses;

public sealed record SaveCoursePlan(string Name, int CertificateLevelId, int DurationMonths, int SessionsPerWeek,
    int? SessionMinutes, int? TotalSessions, int? RetakeClaimDays, decimal Price, bool IsPublished, int Revision = 1,
    string SessionType = "Individual", int Capacity = 1, DateTime? TargetExamDate = null);
public sealed record PurchaseCourse(int PlanId, Guid IdempotencyKey, int? CohortId = null);
public sealed record ActivateCourse(int MentorId, DateTime StartsAt);
public sealed record ScheduleCourseSession(int Number, DateTime ScheduledAt, string? MeetingUrl);
public sealed record ConfirmCourseAttendance(string Status, string Note);
public sealed record AssignCourseHomework(string Title, string Instructions, bool Required, DateTime DueAt);
public sealed record SubmitCourseHomework(string Content);
public sealed record GradeCourseHomework(bool Passed, string Feedback);
public sealed record ReviewCourseRetake(bool Approve, bool OfficialFailureVerified, string Response,
    bool OnTimeHomeworkEvidenceVerified = false, int? CohortId = null);
public sealed record SaveStudyCohort(int PlanId, string Name, int MentorId, int Capacity,
    List<int> AvailabilityIds, bool Publish, int Revision = 1);
public sealed record BookCourseSlot(int AvailabilityId);
public sealed record SaveCourseCertificate(string Name, string? Description, int SortOrder, bool IsActive);
public sealed record SaveCourseLevel(int CertificateTypeId, string Code, string? Description, int SortOrder, bool IsActive);

public sealed record ReviewJlptExam(DateOnly ExamDate, string Location, bool Approve, string Note);

public sealed record RequestCourseChange(int SessionId, string Kind, string Reason);
public sealed record ReviewCourseChange(bool Approve, string Response, int? NewAvailabilityId = null, bool RestoreSessionCredit = false, bool EntireClassConfirmed = false);
