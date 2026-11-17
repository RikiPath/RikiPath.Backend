namespace RikiPath.Application.Responses.Homework;

public sealed record HomeworkLearner(int LearnerId, string LearnerName, int BookingId, DateTime SessionAt);
public sealed record HomeworkExercise(int Id, string Title, string? Description, int QuestionCount, double? ScorePercent);
public sealed record HomeworkLesson(int Id, string Title, string Level, string Skill);
public sealed record HomeworkCatalogItem(HomeworkLesson Lesson, List<HomeworkExercise> Exercises);
// PR #70 (teammate): HomeworkGrade là điểm đã công bố từ GradingResult; giữ RevisionCount/LastRevisedAtUtc của Task 02.
public sealed record HomeworkGrade(double Score, string Feedback, DateTime PublishedAt);
public sealed record HomeworkSubmissionDetail(int Id, string? TextContent, string? FileUrl, DateTime SubmittedAt,
    int RevisionCount = 1, DateTime? LastRevisedAtUtc = null, bool HasScan = false, HomeworkGrade? Grade = null);
public sealed record HomeworkAiDraftView(string? Feedback, double? SuggestedScore, DateTime? GeneratedAtUtc, string? UnavailableReason);
public sealed record HomeworkPublishedGradeView(bool? Passed, double? Score, string? Feedback, DateTime PublishedAtUtc, int? MentorId);
public sealed record HomeworkItem(int Id, int LearnerId, string LearnerName, string MentorName, string Title,
    string? Instructions, DateTime? AssignedAt, DateTime? DueAt, string Status, List<HomeworkLesson> Lessons,
    List<HomeworkExercise> Exercises, HomeworkSubmissionDetail? Submission,
    int? CohortId = null, string AssignmentScope = "Booking1on1", Guid? CohortAssignmentGroupKey = null,
    HomeworkAiDraftView? AiDraft = null, HomeworkPublishedGradeView? PublishedGrade = null, int? BookingId = null);
public sealed record HomeworkPeriodReport(DateOnly From, DateOnly To, int AssignedCount, int SubmittedCount,
    double? HomeworkPercent, int SessionCount, int AttendedCount, double? AttendancePercent, int UntrackedSessionCount);
