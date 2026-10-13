using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Content
{
    public class ImportRowDiffResponse
    {
        public int SourceRowNumber { get; set; }

        /// <summary>Khóa tự nhiên dùng để so khớp (Character / Word / Title...), hiển thị cho admin dễ nhận diện dòng.</summary>
        public string RowKey { get; set; } = string.Empty;

        public ImportRowAction Action { get; set; }

        /// <summary>Id bản ghi đang có trong bảng chính - null nếu Action = New.</summary>
        public int? TargetEntityId { get; set; }

        /// <summary>Toàn bộ field đã parse từ Excel (giá trị MỚI), key là tên field chuẩn hoá (không khoảng trắng, lowercase).</summary>
        public Dictionary<string, string?> ParsedFields { get; set; } = new();

        /// <summary>Chỉ có khi Action = Modified: field -> "giá trị cũ → giá trị mới".</summary>
        public Dictionary<string, string>? ChangedFields { get; set; }

        /// <summary>Chỉ có khi Action = Error.</summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Payload JSON server đã build sẵn cho dòng này (chỉ có khi Action = New/Modified).
        /// FE KHÔNG cần đọc hay sửa field này - chỉ cần gửi y nguyên lại ở Step 2 (ConfirmImportRequest)
        /// cho đúng những dòng admin chọn giữ lại, để server khỏi phải parse lại Excel lần nữa.
        /// </summary>
        public string? PayloadJson { get; set; }
    }
}

