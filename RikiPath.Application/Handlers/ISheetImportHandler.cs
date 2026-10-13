using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Handlers
{
    public interface ISheetImportHandler
    {
        ImportEntityType EntityType { get; }

        /// <summary>Tên sheet "chuẩn" cần tìm trong file - so khớp không phân biệt hoa/thường và khoảng trắng.</summary>
        string SheetName { get; }

        /// <summary>So khớp từng dòng Excel đã đọc với dữ liệu hiện có trong DB, KHÔNG được ghi gì xuống DB ở đây.</summary>
        Task<SheetImportResponse> BuildDiffAsync(IReadOnlyList<ExcelRow> rows, CancellationToken cancellationToken);
    }

}
