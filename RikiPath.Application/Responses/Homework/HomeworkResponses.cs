namespace RikiPath.Application.Responses.Homework;

public sealed record HomeworkLearner(int LearnerId, string LearnerName, int BookingId, DateTime SessionAt);
public sealed record HomeworkExercise(int Id, string Title, string? Description, int QuestionCount, double? ScorePercent);
public sealed record HomeworkLesson(int Id, string Title, string Level, string Skill);
public sealed record HomeworkCatalogItem(HomeworkLesson Lesson, List<HomeworkExercise> Exercises);
public sealed record HomeworkGrade(double Score, string Feedback, DateTime PublishedAt);
public sealed record HomeworkSubmissionDetail(int Id, string? TextContent, string? FileUrl, DateTime SubmittedAt,
    bool HasScan = false, HomeworkGrade? Grade = null);
public sealed record HomeworkItem(int Id, int LearnerId, string LearnerName, string MentorName, string Title,
    string? Instructions, DateTime? AssignedAt, DateTime? DueAt, string Status, List<HomeworkLesson> Lessons,
    List<HomeworkExercise> Exercises, HomeworkSubmissionDetail? Submission, int? BookingId = null);
public sealed record HomeworkPeriodReport(DateOnly From, DateOnly To, int AssignedCount, int SubmittedCount,
    double? HomeworkPercent, int SessionCount, int AttendedCount, double? AttendancePercent, int UntrackedSessionCount);
