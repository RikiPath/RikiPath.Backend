using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorQuality;

// Thông tin cơ bản của đơn khiếu nại tranh chấp buổi tư vấn
public sealed class MentorDisputeResponse
{
    public int Id { get; set; }
    public int MentorBookingId { get; set; }
    public int LearnerId { get; set; }
    public string LearnerName { get; set; } = string.Empty;
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public MentorDisputeCategory Category { get; set; }
    public MentorDisputeStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public string? MentorResponse { get; set; }
    public DateTime? MentorRespondedAtUtc { get; set; }
    public string? AdminNotes { get; set; }
    public string? AdminDecision { get; set; }
    public bool? IsRefundApproved { get; set; }
    public bool CreditRefunded { get; set; }
    public string? RefundOperationKey { get; set; }
    public int? ResolvedByAdminId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

// Chi tiết hồ sơ khiếu nại kèm bằng chứng điểm danh và lịch sử xử lý
public sealed class MentorDisputeDetailResponse
{
    public MentorDisputeResponse Dispute { get; set; } = null!;

    // Thông tin lịch hẹn
    public DateTime? BookingScheduledAt { get; set; }
    public MentorStatus BookingStatus { get; set; }

    // Dữ liệu chứng thực điểm danh và tham gia phòng họp (Stage 09 Evidence)
    public DateTime? AttendanceFirstJoinedAtUtc { get; set; }
    public DateTime? AttendanceLastJoinedAtUtc { get; set; }
    public int AttendanceTotalDurationSeconds { get; set; }
    public int AttendanceReconnectionCount { get; set; }

    // Dữ liệu hủy lịch nếu có (Stage 10 Evidence)
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancelledByRole { get; set; }
    public bool CreditAlreadyRefunded { get; set; }

    // Nhật ký kiểm toán lịch sử thay đổi trạng thái
    public List<MentorDisputeHistoryResponse> Histories { get; set; } = new();
}

// Bản ghi lịch sử kiểm toán xử lý khiếu nại
public sealed class MentorDisputeHistoryResponse
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public MentorDisputeStatus? PreviousStatus { get; set; }
    public MentorDisputeStatus NewStatus { get; set; }
    public int ActorId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
