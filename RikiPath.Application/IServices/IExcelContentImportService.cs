using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices
{
    public interface IExcelContentImportService
    {
        /// <summary>
        /// Step 1: đọc và so sánh nội dung; file media đính kèm được tải lên storage trước khi dựng bản nháp.
        /// onlyType = null -> Bulk upload (đọc mọi sheet handler hỗ trợ
        /// mà file có); onlyType có giá trị -> Single upload (chỉ đọc đúng 1 sheet tương ứng, bỏ qua sheet khác dù file có).
        /// </summary>
        Task<ApiResponse<ImportPreviewResponse>> PreviewAsync(
            Stream fileStream, string fileName, ImportEntityType? onlyType, CancellationToken cancellationToken,
            Func<string, CancellationToken, Task<string>>? uploadLocalAudio = null,
            Func<string, CancellationToken, Task<string>>? uploadLocalImage = null);

        ApiResponse<WorkbookPreviewResponse> PreviewWorkbook(Stream fileStream, string fileName, CancellationToken cancellationToken);
        ApiResponse<byte[]> EditWorkbook(Stream fileStream, IReadOnlyList<WorkbookCellEdit> edits, CancellationToken cancellationToken);

        /// <summary>Lưu các dòng New/Modified thành bản nháp của tác giả.</summary>
        Task<ApiResponse<ImportConfirmResponse>> ConfirmAsync(
            ConfirmImportRequest request, int actorUserId, CancellationToken cancellationToken);
    }
}
