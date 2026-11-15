using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.MentorQuality;

// Học viên nộp đơn khiếu nại / tranh chấp đối với lịch hẹn tư vấn
public sealed class CreateMentorDisputeRequest
{
    public int MentorBookingId { get; set; }
    public MentorDisputeCategory Category { get; set; } = MentorDisputeCategory.Other;
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
}

// Chuyên gia tư vấn gửi bản giải trình phản hồi khiếu nại
public sealed class MentorResponseDisputeRequest
{
    public string ResponseText { get; set; } = string.Empty;
}

// Quản trị viên ra phán quyết giải quyết tranh chấp (hoàn vé hoặc bác bỏ)
public sealed class ResolveMentorDisputeRequest
{
    // Đồng ý hoàn trả 1 lượt tín dụng cho học viên
    public bool ApproveRefund { get; set; }

    // Quyết định chính thức (ví dụ: RefundApproved, DisputeRejected, WarningIssued)
    public string AdminDecision { get; set; } = string.Empty;

    // Ghi chú chi tiết kết luận của Quản trị viên
    public string? AdminNotes { get; set; }

    // Số ngày gia hạn thời hạn gói học khi được hoàn vé (mặc định 7 ngày)
    public int ExtendDays { get; set; } = 7;
}

// Quản trị viên cập nhật trạng thái vòng đời xử lý khiếu nại
public sealed class UpdateDisputeStatusRequest
{
    public MentorDisputeStatus NewStatus { get; set; }
    public string? Notes { get; set; }
}
