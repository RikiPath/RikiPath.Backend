namespace RikiPath.Application.Responses.Homework;

public sealed record HomeworkLearner(int LearnerId, string LearnerName, int BookingId, DateTime SessionAt, int? AvailabilityId = null);
public sealed record HomeworkExercise(int Id, string Title, string? Description, int QuestionCount, double? ScorePercent);
public sealed record HomeworkLesson(int Id, string Title, string Level, string Skill);
public sealed record HomeworkCatalogItem(HomeworkLesson Lesson, List<HomeworkExercise> Exercises);
public sealed record HomeworkAuthoredQuestion(string Question, string[] Options, int? CorrectOptionIndex = null, string? Explanation = null);
public sealed record HomeworkCorrection(string Category, string Original, string Reason, string Correction, string? Location = null);
public sealed record HomeworkAiGradeDraft(int SubmissionId, decimal Score, string Feedback, string? Transcription,
    IReadOnlyList<HomeworkCorrection> Corrections, bool HasUnlistedQuizErrors, IReadOnlyList<string> GradingLevels);
public sealed record HomeworkAiBatchItem(int AssignmentId, HomeworkAiGradeDraft? Draft, string? ErrorMessage);
public sealed record HomeworkGrade(double Score, string Feedback, DateTime PublishedAt, IReadOnlyList<HomeworkCorrection>? Corrections = null);
public sealed record HomeworkSubmissionDetail(int Id, string? TextContent, string? FileUrl, DateTime SubmittedAt,
    bool HasScan = false, HomeworkGrade? Grade = null, bool HasAudio = false, int[]? SelectedOptions = null);
public sealed record HomeworkItem(int Id, int LearnerId, string LearnerName, string MentorName, string Title,
    string? Instructions, DateTime? AssignedAt, DateTime? DueAt, string Status, List<HomeworkLesson> Lessons,
    List<HomeworkExercise> Exercises, HomeworkSubmissionDetail? Submission, int? BookingId = null,
    string? AssignmentType = null, string? JlptLevel = null, IReadOnlyList<HomeworkAuthoredQuestion>? Questions = null);
public sealed record HomeworkPeriodReport(DateOnly From, DateOnly To, int AssignedCount, int SubmittedCount,
    double? HomeworkPercent, int SessionCount, int AttendedCount, double? AttendancePercent, int UntrackedSessionCount);
