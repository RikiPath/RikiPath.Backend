namespace RikiPath.Application.Responses.Homework;

public sealed record HomeworkLearner(int LearnerId, string LearnerName, int BookingId, DateTime SessionAt, int? AvailabilityId = null);
public sealed record HomeworkExercise(int Id, string Title, string? Description, int QuestionCount, double? ScorePercent);
public sealed record HomeworkLesson(int Id, string Title, string Level, string Skill);
public sealed record HomeworkCatalogItem(HomeworkLesson Lesson, List<HomeworkExercise> Exercises);

// Các record phục vụ bài tập tự soạn (Trắc nghiệm / Viết / Nói) và chấm AI
public sealed record HomeworkAuthoredQuestion(string Question, string[] Options, int? CorrectOptionIndex = null, string? Explanation = null);
public sealed record HomeworkCorrection(string Category, string Original, string Reason, string Correction, string? Location = null);
public sealed record HomeworkAiGradeDraft(int SubmissionId, decimal Score, string Feedback, string? Transcription,
    IReadOnlyList<HomeworkCorrection> Corrections, bool HasUnlistedQuizErrors, IReadOnlyList<string> GradingLevels);
public sealed record HomeworkAiBatchItem(int AssignmentId, HomeworkAiGradeDraft? Draft, string? ErrorMessage);

// PR #70 (teammate) + Task 02 + Authored Homework: đã gộp đầy đủ tham số của cả hai nhánh
public sealed record HomeworkGrade(double Score, string Feedback, DateTime PublishedAt, IReadOnlyList<HomeworkCorrection>? Corrections = null);
public sealed record HomeworkSubmissionDetail(int Id, string? TextContent, string? FileUrl, DateTime SubmittedAt,
    int RevisionCount = 1, DateTime? LastRevisedAtUtc = null, bool HasScan = false, HomeworkGrade? Grade = null,
    bool HasAudio = false, int[]? SelectedOptions = null);
public sealed record HomeworkAiDraftView(string? Feedback, double? SuggestedScore, DateTime? GeneratedAtUtc, string? UnavailableReason);
public sealed record HomeworkPublishedGradeView(bool? Passed, double? Score, string? Feedback, DateTime PublishedAtUtc, int? MentorId);
public sealed record HomeworkItem(int Id, int LearnerId, string LearnerName, string MentorName, string Title,
    string? Instructions, DateTime? AssignedAt, DateTime? DueAt, string Status, List<HomeworkLesson> Lessons,
    List<HomeworkExercise> Exercises, HomeworkSubmissionDetail? Submission,
    int? CohortId = null, string AssignmentScope = "Booking1on1", Guid? CohortAssignmentGroupKey = null,
    HomeworkAiDraftView? AiDraft = null, HomeworkPublishedGradeView? PublishedGrade = null, int? BookingId = null,
    string? AssignmentType = null, string? JlptLevel = null, IReadOnlyList<HomeworkAuthoredQuestion>? Questions = null);

public sealed record HomeworkPeriodReport(DateOnly From, DateOnly To, int AssignedCount, int SubmittedCount,
    double? HomeworkPercent, int SessionCount, int AttendedCount, double? AttendancePercent, int UntrackedSessionCount);