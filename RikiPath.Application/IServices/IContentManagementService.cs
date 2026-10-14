using RikiPath.Application.DTOs.Content;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices
{
    public interface IContentManagementService
    {
        /// <summary>
        /// Import hàng loạt nội dung (Lesson, Kanji, Vocabulary, GrammarPattern, PracticeExercise...) từ file Excel.
        /// </summary>
        Task<ApiResponse<BulkImportResultResponse>> BulkImportAsync(
            BulkImportRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách nội dung đang chờ duyệt theo loại entity.
        /// </summary>
        Task<ApiResponse<List<ContentReviewStatusResponse>>> GetPendingReviewAsync(
            ContentEntityType entityType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Thực hiện phê duyệt hoặc từ chối một nội dung cụ thể.
        /// </summary>
        Task<ApiResponse<ContentReviewStatusResponse>> ReviewAsync(
            ContentEntityType entityType,
            int entityId,
            ReviewContentRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi nội dung lên hệ thống để chờ kiểm duyệt.
        /// </summary>
        Task<ApiResponse<bool>> SubmitForReviewAsync(
            ContentEntityType entityType,
            int entityId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách nội dung do chính tác giả hiện tại khởi tạo.
        /// </summary>
        Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(
            ContentEntityType entityType,
            CancellationToken cancellationToken = default);
    }
}