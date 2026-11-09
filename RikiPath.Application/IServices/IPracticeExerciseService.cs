using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeExercises;

namespace RikiPath.Application.IServices
{
    public interface IPracticeExerciseService
    {
        Task<ApiResponse<List<PracticeExerciseSummaryResponse>>> GetByLessonAsync(
            int lessonId,
            int userId,
            CancellationToken cancellationToken);

        Task<ApiResponse<StartPracticeAttemptResponse>> StartAttemptAsync(
            int practiceExerciseId,
            int userId,
            CancellationToken cancellationToken);

        Task<ApiResponse<SubmitPracticeAttemptResponse>> SubmitAttemptAsync(
            int attemptId,
            int userId,
            SubmitPracticeAttemptRequest request,
            CancellationToken cancellationToken);
    }
}
