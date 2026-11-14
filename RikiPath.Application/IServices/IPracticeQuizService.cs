using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeExercises;

namespace RikiPath.Application.IServices;

public interface IPracticeQuizService
{
    Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> GetLessonExercisesAsync(
        int lessonId, CancellationToken cancellationToken);

    Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> GetAssessmentRoadmapExercisesAsync(
        int lessonId, CancellationToken cancellationToken);

    Task<ApiResponse<PracticeExerciseAttemptResponse>> StartAttemptAsync(
        int practiceExerciseId, CancellationToken cancellationToken, bool fromCatalog = false, string? skill = null);

    Task<ApiResponse<PracticeExerciseAttemptResponse>> StartAssessmentRoadmapAttemptAsync(
        int practiceExerciseId, CancellationToken cancellationToken);

    Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitAttemptAsync(
        int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitAssessmentRoadmapAttemptAsync(
        int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<PracticeExerciseAttemptResponse>> StartHomeworkAttemptAsync(
        int assignmentId, int exerciseId, CancellationToken cancellationToken);
    Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitHomeworkAttemptAsync(
        int assignmentId, int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken);
}
