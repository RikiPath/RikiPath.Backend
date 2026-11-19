using System.ComponentModel.DataAnnotations;
using RikiPath.Application.Responses.Homework;

namespace RikiPath.Application.Requests.Homework;

public record HomeworkContentRequest(
    [property: Required, MaxLength(300)] string Title,
    [property: MaxLength(10000)] string? Instructions,
    DateTimeOffset? DueAt,
    [property: MaxLength(30)] int[]? LessonIds,
    [property: MaxLength(30)] int[]? PracticeExerciseIds,
    [property: MaxLength(20)] string? AssignmentType = null,
    [property: MaxLength(8)] string? JlptLevel = null,
    [property: MaxLength(20)] CreateHomeworkQuestionRequest[]? Questions = null);

public sealed record CreateHomeworkRequest(
    [property: Range(1, int.MaxValue)] int BookingId,
    string Title,
    string? Instructions,
    DateTimeOffset? DueAt,
    int[]? LessonIds,
    int[]? PracticeExerciseIds,
    string? AssignmentType = null,
    string? JlptLevel = null,
    CreateHomeworkQuestionRequest[]? Questions = null)
    : HomeworkContentRequest(Title, Instructions, DueAt, LessonIds, PracticeExerciseIds, AssignmentType, JlptLevel, Questions);

public sealed record CreateHomeworkGroupRequest(
    [property: Required, MinLength(2)] int[] BookingIds,
    [property: Required] HomeworkContentRequest Homework);

public sealed record CreateHomeworkQuestionRequest(
    [property: Required, MaxLength(1000)] string Question,
    [property: Required, MinLength(2), MaxLength(4)] string[] Options,
    int? CorrectOptionIndex,
    [property: MaxLength(800)] string? Explanation = null);

public sealed record SubmitHomeworkRequest(
    [property: MaxLength(20000)] string? TextContent,
    [property: MaxLength(2000)] string? FileUrl,
    [property: MaxLength(20)] int[]? SelectedOptions = null);

public sealed record HomeworkAiDraftRequest(
    [property: MaxLength(8)] string? JlptLevel = null,
    [property: MaxLength(1200)] string? MentorPrompt = null);

public sealed record HomeworkAiBatchDraftRequest(
    [property: Required, MinLength(1), MaxLength(4)] int[] AssignmentIds,
    [property: Required, MaxLength(1200)] string MentorPrompt,
    [property: MaxLength(8)] string? JlptLevel = null);

public sealed record PublishHomeworkGradeRequest(
    [property: Range(1, int.MaxValue)] int SubmissionId,
    [property: Range(0, 100)] decimal Score,
    [property: Required, MaxLength(2000)] string Feedback,
    [property: MaxLength(20)] HomeworkCorrection[]? Corrections = null);

public sealed record EvaluateMentorBookingRequest(
    [property: Range(0, 100)] int Score,
    [property: Required, MaxLength(2000)] string Feedback);

public sealed record RecordMentorPracticeResultRequest([property: Range(1, int.MaxValue)] int AttemptId);

public sealed record SaveMentorReviewPlanRequest(
    [property: Required, MaxLength(6000)] string Plan, DateTimeOffset? SessionAt, bool Completed = false);
