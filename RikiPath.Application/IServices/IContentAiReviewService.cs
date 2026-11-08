using RikiPath.Application.DTOs.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Application.Responses.ContentReview;

namespace RikiPath.Application.IServices
{
    public interface IContentAiReviewService
    {
        Task<ApiResponse<ContentAiReviewResponse>> ReviewAsync(
            ContentEntityType type, int contentId, AuthorContentDetailsResponse? staged, CancellationToken cancellationToken);

        Task<ApiResponse<ContentAiReviewResponse?>> GetLatestAsync(
            ContentEntityType type, int contentId, CancellationToken cancellationToken);
    }
}