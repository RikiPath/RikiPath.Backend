using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Homework;

public sealed record CreateHomeworkRequest(
    [Range(1, int.MaxValue)] int? BookingId,
    [Required, MaxLength(300)] string Title,
    [MaxLength(10000)] string? Instructions,
    DateTimeOffset? DueAt,
    [MaxLength(30)] int[]? LessonIds,
    [MaxLength(30)] int[]? PracticeExerciseIds,
    [Range(1, int.MaxValue)] int? CohortId = null,
    List<int>? TargetLearnerIds = null);

public sealed record SubmitHomeworkRequest(
    [MaxLength(20000)] string? TextContent,
    [MaxLength(2000)] string? FileUrl);

public sealed record GradeHomeworkAssignmentRequest(
    bool Passed,
    [Required, MaxLength(4000)] string Feedback,
    [Range(0, 100)] double? Score = null,
    bool PublishAiDraft = false);

public sealed record PublishHomeworkGradeRequest(
    [property: Range(1, int.MaxValue)] int SubmissionId,
    [property: Range(0, 100)] decimal Score,
    [property: Required, MaxLength(2000)] string Feedback);

public sealed record EvaluateMentorBookingRequest(
    [property: Range(0, 100)] int Score,
    [property: Required, MaxLength(2000)] string Feedback);

public sealed record RecordMentorPracticeResultRequest([property: Range(1, int.MaxValue)] int AttemptId);

public sealed record SaveMentorReviewPlanRequest(
    [property: Required, MaxLength(6000)] string Plan, DateTimeOffset? SessionAt, bool Completed = false);
