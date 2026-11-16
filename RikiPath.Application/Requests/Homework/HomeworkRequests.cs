using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Homework;

public sealed record CreateHomeworkRequest(
    [property: Range(1, int.MaxValue)] int BookingId,
    [property: Required, MaxLength(300)] string Title,
    [property: MaxLength(10000)] string? Instructions,
    DateTimeOffset? DueAt,
    [property: MaxLength(30)] int[]? LessonIds,
    [property: MaxLength(30)] int[]? PracticeExerciseIds);

public sealed record SubmitHomeworkRequest(
    [property: MaxLength(20000)] string? TextContent,
    [property: MaxLength(2000)] string? FileUrl);

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
