using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

// Nhật ký kiểm toán lịch sử xử lý và chuyển đổi trạng thái khiếu nại Mentor
public sealed class MentorDisputeHistory
{
    public long Id { get; set; }

    // Đơn khiếu nại liên kết
    public int DisputeId { get; set; }
    public MentorDispute Dispute { get; set; } = null!;

    // Hành động thực hiện (Created, StatusChanged, MentorReplied, AdminResolved)
    public string Action { get; set; } = string.Empty;

    // Trạng thái trước khi chuyển đổi
    public MentorDisputeStatus? PreviousStatus { get; set; }

    // Trạng thái mới sau khi chuyển đổi
    public MentorDisputeStatus NewStatus { get; set; }

    // Người thực hiện hành động
    public int ActorId { get; set; }
    public string ActorRole { get; set; } = string.Empty;

    // Ghi chú đính kèm theo hành động
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
