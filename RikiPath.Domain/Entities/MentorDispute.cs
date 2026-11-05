using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

// Đơn khiếu nại và tranh chấp phát sinh từ buổi tư vấn Mentor 1-1
public sealed class MentorDispute
{
    public int Id { get; set; }

    // Lịch hẹn tư vấn phát sinh tranh chấp
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;

    // Học viên khởi tạo khiếu nại
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;

    // Chuyên gia bị khiếu nại
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;

    // Phân loại nhóm sự cố / khiếu nại
    public MentorDisputeCategory Category { get; set; } = MentorDisputeCategory.Other;

    // Trạng thái vòng đời xử lý tranh chấp
    public MentorDisputeStatus Status { get; set; } = MentorDisputeStatus.Submitted;

    // Nội dung trình bày lý do khiếu nại từ học viên
    public string Reason { get; set; } = string.Empty;

    // Đường dẫn bằng chứng đính kèm (ảnh chụp, video sự cố)
    public string? EvidenceUrls { get; set; }

    // Phản hồi và giải trình từ chuyên gia tư vấn
    public string? MentorResponse { get; set; }
    public DateTime? MentorRespondedAtUtc { get; set; }

    // Ghi chú thẩm tra và kết luận của Quản trị viên
    public string? AdminNotes { get; set; }

    // Quyết định xử lý của Quản trị viên
    public string? AdminDecision { get; set; }

    // Cờ chấp thuận hoàn trả lượt tín dụng cho học viên
    public bool? IsRefundApproved { get; set; }

    // Đánh dấu lượt tín dụng đã được hoàn trả thành công qua Credit Ledger
    public bool CreditRefunded { get; set; }

    // Khóa idempotency của bút toán hoàn tín dụng
    public string? RefundOperationKey { get; set; }

    // Quản trị viên thụ lý và ra quyết định
    public int? ResolvedByAdminId { get; set; }
    public UserAccount? ResolvedByAdmin { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Lịch sử chuyển đổi trạng thái phục vụ kiểm toán
    public ICollection<MentorDisputeHistory> Histories { get; set; } = new List<MentorDisputeHistory>();
}
