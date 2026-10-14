using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Content
{
    public class ContentApprovalListItemResponse
    {
        public int Id { get; set; }
        public Guid BatchId { get; set; }
        public ImportEntityType EntityType { get; set; }
        /// <summary>New (thêm mới) hoặc Modified (cập nhật).</summary>
        public ImportRowAction Action { get; set; }
        public int? TargetEntityId { get; set; }
        public string RowKey { get; set; } = string.Empty;
        public ContentApprovalStatus Status { get; set; }
        public string SourceFileName { get; set; } = string.Empty;
        public int SourceRowNumber { get; set; }
        public int SubmittedByUserId { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        /// <summary>Số trường thay đổi theo bản tóm tắt lưu lúc import (null nếu không có).</summary>
        public int? ChangedFieldCount { get; set; }
    }
}
