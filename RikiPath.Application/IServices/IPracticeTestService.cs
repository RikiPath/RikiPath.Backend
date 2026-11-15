using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;

namespace RikiPath.Application.IServices
{
    public interface IPracticeTestService
    {
        Task<ApiResponse<PlacementTestStatusResponse>> GetStatusAsync(CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestStatusResponse>> SkipPlacementAsync(CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse?>> GetActiveAttemptAsync(CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAnswersSavedResponse>> SavePlacementAnswersAsync(
            int attemptId, SubmitPlacementTestRequest request, CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            StartPlacementTestRequest? request, CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestResultResponse>> SubmitPlacementTestAsync(
            int attemptId, SubmitPlacementTestRequest request, CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestResultResponse>> GetPlacementTestResultAsync(
            int attemptId, CancellationToken cancellationToken = default);

        Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(int practiceTestId, CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(int attemptId, CancellationToken cancellationToken = default);

        Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(int jlptLevelId, CancellationToken cancellationToken = default);

        Task<ApiResponse<MockTestDetailResponse>> GetPlacementTestDetailAsync(int certificateLevelId, CancellationToken cancellationToken = default);

        Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(int attemptId, CancellationToken cancellationToken = default);

        Task<ApiResponse<List<QuestionReviewCard>>> GetWrongQuestionsReviewSessionAsync(int? practiceTestId = null, CancellationToken cancellationToken = default);


    }
}
