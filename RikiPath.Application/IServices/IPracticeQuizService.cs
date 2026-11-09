using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeExercises;

namespace RikiPath.Application.IServices;

public interface IPracticeQuizService
{
    Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> GetLessonExercisesAsync(
        int lessonId, CancellationToken cancellationToken);

    Task<ApiResponse<PracticeExerciseAttemptResponse>> StartAttemptAsync(
        int practiceExerciseId, CancellationToken cancellationToken);

    Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitAttemptAsync(
        int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken);
}
