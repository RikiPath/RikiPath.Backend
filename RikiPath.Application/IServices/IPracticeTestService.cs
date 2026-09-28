using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;

namespace RikiPath.Application.IServices
{
    public interface IMockTestService
    {
        Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(int practiceTestId, CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(int attemptId, CancellationToken cancellationToken = default);

        Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(int jlptLevelId, CancellationToken cancellationToken = default);

        Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(int attemptId, CancellationToken cancellationToken = default);

        Task<ApiResponse<List<QuestionReviewCard>>> GetWrongQuestionsReviewSessionAsync(int? practiceTestId = null, CancellationToken cancellationToken = default);
    }
}
