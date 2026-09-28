using RikiPath.Application.Common;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.IServices
{
    public interface IContentManagementService
    {
        // Bulk import: Kanji / Vocabulary / GrammarPattern / MockQuestion
        Task<ApiResponse<BulkImportResultResponse>> BulkImportAsync(BulkImportRequest request, CancellationToken cancellationToken);

        // Author: gửi duyệt 1 entity (Course/Kanji/Vocabulary/GrammarPattern/MockTest)
        Task<ApiResponse<ContentReviewStatusResponse>> SubmitForReviewAsync(ContentEntityType entityType, int entityId, CancellationToken cancellationToken);

        Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(ContentEntityType entityType, CancellationToken cancellationToken);

        // Admin
        Task<ApiResponse<List<ContentReviewStatusResponse>>> GetPendingReviewAsync(
            ContentEntityType entityType, CancellationToken cancellationToken);

        Task<ApiResponse<ContentReviewStatusResponse>> ReviewAsync(ContentEntityType entityType, int entityId, ReviewContentRequest request, CancellationToken cancellationToken);
    }
}
