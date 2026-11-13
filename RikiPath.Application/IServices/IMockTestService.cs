using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;

namespace RikiPath.Application.IServices
{
    public interface IMockTestService
    {
        Task<ApiResponse<PlacementTestStatusResponse>> GetStatusAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestStatusResponse>> SkipPlacementAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse?>> GetActiveAttemptAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAnswersSavedResponse>> SavePlacementAnswersAsync(
            int attemptId,
            SubmitPlacementTestRequest request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            StartPlacementTestRequest? request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestResultResponse>> SubmitPlacementTestAsync(
            int attemptId,
            SubmitPlacementTestRequest request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PlacementTestResultResponse>> GetPlacementTestResultAsync(
            int attemptId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Autosave 1 câu trả lời cho bài đánh giá năng lực (placement) ngay khi học viên chọn đáp án,
        /// để nếu mất điện/tắt web giữa chừng thì quay lại vẫn còn tiến độ đã làm.
        /// </summary>
        Task<ApiResponse<PlacementAnswerAckResponse>> SavePlacementAnswerAsync(
            int attemptId,
            SavePlacementAnswerRequest request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<MockTestDetailResponse>> GetPlacementTestsByCertificateLevelAsync(
            int certificateLevelId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(
            int practiceTestId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<StartAttemptResponse?>> GetActiveMockAttemptAsync(
            int practiceTestId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(
            int attemptId,
            SubmitAttemptRequest request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<bool>> SaveAttemptAnswerAsync(
            int attemptId,
            AnswerSubmissionItem answer,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> FinalizeExpiredAttemptAsync(
            int attemptId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(
            int attemptId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(
            int certificationLevelId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<MockTestDetailResponse>> GetPlacementTestDetailAsync(
            int certificateLevelId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(
            int attemptId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<List<QuestionReviewCard>>> GetWrongQuestionsReviewSessionAsync(
            int? practiceTestId = null,
            CancellationToken cancellationToken = default);
    }
}
