using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices
{
    public interface IExcelContentImportService
    {
        /// <summary>
        /// Step 1 - Dry Run. Không ghi gì xuống DB. onlyType = null -> Bulk upload (đọc mọi sheet handler hỗ trợ
        /// mà file có); onlyType có giá trị -> Single upload (chỉ đọc đúng 1 sheet tương ứng, bỏ qua sheet khác dù file có).
        /// </summary>
        Task<ApiResponse<ImportPreviewResponse>> PreviewAsync(
            Stream fileStream, string fileName, ImportEntityType? onlyType, CancellationToken cancellationToken);

        /// <summary>Step 2 + 3 - Tạo bản nháp (ContentApprovalRequest) cho các dòng New/Modified đã được xác nhận, rồi báo Admin.</summary>
        Task<ApiResponse<ImportConfirmResponse>> ConfirmAsync(
            ConfirmImportRequest request, int actorUserId, CancellationToken cancellationToken);
    }
}
