using RikiPath.Application.Requests.Reviews;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Reviews;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices
{
    public interface IReviewService
    {
        Task<ApiResponse<SrsSummaryResponse>> GetSummaryAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<DailyReviewQueueResponse>> GetDailyReviewQueueAsync(
            int userId,
            string mode,
            string? cardType,
            string? jlptLevel,
            string? sourceType,
            CancellationToken cancellationToken);

        Task<ApiResponse<SrsCardLibraryResponse>> GetCardLibraryAsync(
            int userId,
            SrsCardStatus? status,
            SrsLearningState? learningState,
            string? cardType,
            string? jlptLevel,
            string? sourceType,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken);

        Task<ApiResponse<ReviewQueueItemResponse>> GetCardByIdAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken);

        Task<ApiResponse<List<SrsReviewHistoryItemResponse>>> GetCardHistoryAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken);

        Task<ApiResponse<ReviewQueueItemResponse>> PreviewEnrollAsync(
            int userId,
            EnrollSrsCardRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<SrsEnrollOutcomeResponse>> EnrollCardAsync(
            int userId,
            EnrollSrsCardRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<SrsBatchEnrollResponse>> BatchEnrollCardsAsync(
            int userId,
            BatchEnrollSrsCardsRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<ReviewQueueItemResponse>> UpdateCardAsync(
            int userId,
            int cardId,
            UpdateSrsCardRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<ReviewQueueItemResponse>> SuspendCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken);

        Task<ApiResponse<ReviewQueueItemResponse>> ResumeCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken);

        Task<ApiResponse<ReviewQueueItemResponse>> RestoreCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken);

        Task<ApiResponse<bool>> SoftDeleteCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken);

        Task<ApiResponse<UserSrsPreferenceResponse>> GetPreferencesAsync(
            int userId,
            CancellationToken cancellationToken);

        Task<ApiResponse<UserSrsPreferenceResponse>> UpdatePreferencesAsync(
            int userId,
            UpdateUserSrsPreferenceRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<SubmitReviewResultResponse>> SubmitReviewResultAsync(
            int userId,
            SubmitReviewRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<SrsReviewSessionResponse>> StartSessionAsync(
            int userId,
            StartSrsSessionRequest request,
            CancellationToken cancellationToken);

        Task<ApiResponse<SrsReviewSessionResponse?>> GetActiveSessionAsync(
            int userId,
            string? sessionKind = null,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<SrsReviewSessionResponse>> FinishSessionAsync(
            int userId,
            Guid sessionKey,
            CancellationToken cancellationToken);

        Task<ApiResponse<bool>> AbandonSessionAsync(
            int userId,
            Guid sessionKey,
            CancellationToken cancellationToken);

        Task<ApiResponse<List<SrsCalendarDayResponse>>> GetReviewCalendarAsync(
            int userId,
            int year,
            int month,
            CancellationToken cancellationToken);
    }
}
