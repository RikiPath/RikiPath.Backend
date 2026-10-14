using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Content
{
    public class PendingApprovalItemResponse
    {
        /// <summary>ID của yêu cầu phê duyệt (ContentApprovalRequest.Id).</summary>
        public int RequestId { get; set; }

        /// <summary>Loại nội dung (Vocabulary, Kanji, GrammarPattern...).</summary>
        public ImportEntityType EntityType { get; set; }

        /// <summary>Hành động đề xuất (New: Thêm mới, Modified: Sửa đổi).</summary>
        public ImportRowAction Action { get; set; }

        /// <summary>Khóa định danh hiển thị (Ví dụ: Từ vựng "猫 / ねこ", Kanji "日"...).</summary>
        public string RowKey { get; set; } = string.Empty;

        /// <summary>ID của bản ghi gốc trong DB (nếu là hành động Modified).</summary>
        public int? TargetEntityId { get; set; }

        /// <summary>Tên Content Author gửi yêu cầu.</summary>
        public string SubmittedByName { get; set; } = string.Empty;

        /// <summary>Thời gian gửi yêu cầu.</summary>
        public DateTime SubmittedAt { get; set; }

        /// <summary>Danh sách so sánh chi tiết từng trường dữ liệu (Cũ vs Mới).</summary>
        public List<ApprovalFieldDiffResponse> Fields { get; set; } = new();

        /// <summary>Các cảnh báo hệ thống (VD: Phát hiện trùng lặp, bản ghi gốc bị xóa...).</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Cờ đánh dấu bản ghi gốc trong DB đã bị xóa trong lúc chờ duyệt.</summary>
        public bool TargetMissing { get; set; }
    }
}
