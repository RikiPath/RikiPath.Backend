namespace RikiPath.Domain.Entities;

// Báo cáo vi phạm quy tắc ứng xử hoặc lôi kéo giao dịch ngoài nền tảng
public sealed class MentorAbuseReport
{
    public int Id { get; set; }

    // Người tạo báo cáo (Học viên hoặc Mentor)
    public int ReporterId { get; set; }
    public UserAccount Reporter { get; set; } = null!;

    // Đối tượng bị tố cáo
    public int TargetUserId { get; set; }
    public UserAccount TargetUser { get; set; } = null!;

    // Buổi tư vấn liên quan (nếu có)
    public int? MentorBookingId { get; set; }
    public MentorBooking? MentorBooking { get; set; }

    // Phân loại vi phạm (OffPlatformSolicitation, Harassment, Fraud, Other)
    public string Category { get; set; } = "Other";

    // Mô tả chi tiết hành vi vi phạm
    public string Description { get; set; } = string.Empty;

    // Trạng thái xử lý (Pending, Investigating, Dismissed, ActionTaken)
    public string Status { get; set; } = "Pending";

    // Ghi chú của Quản trị viên
    public string? AdminNotes { get; set; }

    // Quản trị viên xử lý
    public int? ReviewedByAdminId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
