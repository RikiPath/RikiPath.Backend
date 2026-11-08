using Microsoft.AspNetCore.Http;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices
{
    public interface IContentApprovalService
    {
        /// <summary>
        /// Xem trước nội dung đọc từ file Excel import.
        /// </summary>
        Task<ApiResponse<List<SheetImportResponse>>> PreviewImportAsync(
            IFormFile file, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi danh sách các yêu cầu thay đổi/thêm mới từ Excel vào hàng chờ duyệt.
        /// </summary>
        Task<ApiResponse<int>> SubmitImportRequestsAsync(
            List<SheetImportResponse> sheetDiffs, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách các yêu cầu đang chờ duyệt (Pending Review).
        /// </summary>
        Task<ApiResponse<List<PendingApprovalItemResponse>>> GetPendingApprovalsAsync(
            ImportEntityType? entityType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Duyệt hoặc từ chối một yêu cầu duyệt nội dung cụ thể.
        /// </summary>
        Task<ApiResponse<bool>> ReviewRequestAsync(
            int requestId, ReviewContentRequest request, CancellationToken cancellationToken = default);

        Task<ApiResponse<BulkReviewResponse>> ReviewLessonGroupAsync(
            int lessonRequestId, ReviewContentRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Duyệt hoặc từ chối hàng loạt nhiều yêu cầu cùng lúc.
        /// </summary>
        Task<ApiResponse<BulkReviewResponse>> ReviewBatchAsync(
            BulkReviewRequest request, CancellationToken cancellationToken = default);

        Task<ApiResponse<List<ContentApprovalListItemResponse>>> GetMyApprovalsAsync(
            CancellationToken cancellationToken = default);
    }
}
