using RikiPath.Application.Common;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.IServices
{
    public interface IContentManagementService
    {
        // Bulk import: KanjiEntry / VocabularyEntry / GrammarPoint / PracticeQuestion
        Task<ApiResponse<BulkImportResultResponse>> BulkImportAsync(
            int authorId, BulkImportRequest request, CancellationToken cancellationToken);

        // Author: gửi duyệt 1 entity (Course/KanjiEntry/VocabularyEntry/GrammarPoint/PracticeTest)
        Task<ApiResponse<ContentReviewStatusResponse>> SubmitForReviewAsync(
            int authorId, ContentEntityType entityType, int entityId, CancellationToken cancellationToken);

        Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(
            int authorId, ContentEntityType entityType, CancellationToken cancellationToken);

        // Admin
        Task<ApiResponse<List<ContentReviewStatusResponse>>> GetPendingReviewAsync(
            ContentEntityType entityType, CancellationToken cancellationToken);

        Task<ApiResponse<ContentReviewStatusResponse>> ReviewAsync(
            int adminId, ContentEntityType entityType, int entityId, ReviewContentRequest request, CancellationToken cancellationToken);
    }
}
