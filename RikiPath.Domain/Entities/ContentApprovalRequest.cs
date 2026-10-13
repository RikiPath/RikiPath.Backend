using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class ContentApprovalRequest : Base
    {
        public int Id { get; set; }

        /// <summary>Gom các dòng thuộc cùng 1 lần Confirm import (1 lần bấm "Xác nhận" ở Step 2).</summary>
        public Guid BatchId { get; set; }

        public ImportEntityType EntityType { get; set; }

        /// <summary>Luôn là New hoặc Modified - Unchanged/Error không bao giờ được lưu ở đây.</summary>
        public ImportRowAction Action { get; set; }

        /// <summary>Id của bản ghi đang có trong bảng chính - null nếu Action = New.</summary>
        public int? TargetEntityId { get; set; }

        /// <summary>Khóa tự nhiên dùng để so khớp (vd Character của Kanji, Word của Vocabulary...), phục vụ hiển thị & audit.</summary>
        public string RowKey { get; set; } = string.Empty;

        /// <summary>Toàn bộ dữ liệu dòng đã parse + resolve, serialize JSON (khác nhau theo EntityType) - tầng Apply sau này deserialize lại đúng DTO tương ứng.</summary>
        public string PayloadJson { get; set; } = string.Empty;

        /// <summary>Optional: field -> "giá trị cũ => giá trị mới", phục vụ UI admin xem nhanh thay đổi.</summary>
        public string? DiffSummaryJson { get; set; }

        /// <summary>Số thứ tự dòng trong sheet Excel gốc - để truy vết khi có lỗi/khiếu nại.</summary>
        public int SourceRowNumber { get; set; }

        public string SourceFileName { get; set; } = string.Empty;

        public ContentApprovalStatus Status { get; set; } = ContentApprovalStatus.Draft;

        public int SubmittedByUserId { get; set; }
        public UserAccount SubmittedBy { get; set; } = null!;
        public DateTime SubmittedAt { get; set; }

        public string? ReviewedByName { get; set; }

        // Giữ nguyên ngày duyệt và ghi chú:
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
    }
}
