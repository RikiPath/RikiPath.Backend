using RikiPath.Application.Requests.PracticeTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeTests;

namespace RikiPath.Application.IServices
{
    public interface IPracticeTestService
    {
        Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(int practiceTestId, CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(int attemptId, CancellationToken cancellationToken = default);

        Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(int jlptLevelId, CancellationToken cancellationToken = default);

        Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(int attemptId, CancellationToken cancellationToken = default);

        Task<ApiResponse<List<QuestionReviewItem>>> GetWrongQuestionsReviewSessionAsync(int? practiceTestId = null, CancellationToken cancellationToken = default);
    }
}
