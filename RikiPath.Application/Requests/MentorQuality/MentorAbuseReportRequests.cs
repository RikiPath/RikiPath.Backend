namespace RikiPath.Application.Requests.MentorQuality;

// Người dùng gửi báo cáo vi phạm ứng xử hoặc giao dịch ngoài nền tảng
public sealed class CreateMentorAbuseReportRequest
{
    public int TargetUserId { get; set; }
    public int? MentorBookingId { get; set; }
    public string Category { get; set; } = "Other";
    public string Description { get; set; } = string.Empty;
}

// Quản trị viên xử lý báo cáo vi phạm
public sealed class ReviewMentorAbuseReportRequest
{
    public string Status { get; set; } = "Dismissed";
    public string? AdminNotes { get; set; }
}
