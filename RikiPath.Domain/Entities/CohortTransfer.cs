namespace RikiPath.Domain.Entities;

// Yêu cầu chuyển lớp do Học viên gửi; chỉ Admin duyệt mới thay đổi ghi danh. Tách biệt với
// phản hồi/đánh giá Mentor (MentorReview) và khiếu nại (MentorDispute): điểm thấp không tự tạo yêu cầu này.
public sealed class CohortTransferRequest
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    public int Id { get; set; }
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public int SourceCourseId { get; set; }
    public StudyCourse SourceCourse { get; set; } = null!;
    public int SourceCohortId { get; set; }
    public StudyCohort SourceCohort { get; set; } = null!;
    // Lớp Học viên đề xuất (không bắt buộc) và lớp Admin chốt khi duyệt.
    public int? RequestedTargetCohortId { get; set; }
    public StudyCohort? RequestedTargetCohort { get; set; }
    public int? TargetCohortId { get; set; }
    public StudyCohort? TargetCohort { get; set; }
    public string Reason { get; set; } = "";
    public string? EvidenceUrl { get; set; }
    public string Status { get; set; } = Pending;
    public Guid IdempotencyKey { get; set; }
    public int? PolicyId { get; set; }
    public int? PolicyVersionSnapshot { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public int? DestinationCourseId { get; set; }
    public StudyCourse? DestinationCourse { get; set; }
    public string? EligibilitySnapshot { get; set; }
    public int Revision { get; set; } = 1;
    public List<CohortTransferRequestEvent> History { get; set; } = [];
}

// Lịch sử bất biến của yêu cầu chuyển lớp (append-only).
public sealed class CohortTransferRequestEvent
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public CohortTransferRequest Request { get; set; } = null!;
    public string Action { get; set; } = "";
    public int ActorId { get; set; }
    public string ActorRole { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

// Yêu cầu dời toàn bộ lịch (ShiftWeeks) hoặc hủy lớp trống (CancelEmptyCohort) do Mentor chính/Admin gửi.
// Mỗi lần dời là một quyết định Admin riêng, có lịch sử; tối đa một yêu cầu Pending cho mỗi lớp.
public sealed class CohortScheduleRequest
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public const string KindShiftWeeks = "ShiftWeeks";
    public const string KindCancelEmptyCohort = "CancelEmptyCohort";

    public int Id { get; set; }
    public int CohortId { get; set; }
    public StudyCohort Cohort { get; set; } = null!;
    public string Kind { get; set; } = KindShiftWeeks;
    public int? ShiftWeeks { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = Pending;
    public int RequestedById { get; set; }
    public UserAccount RequestedBy { get; set; } = null!;
    public string RequestedByRole { get; set; } = "";
    public Guid IdempotencyKey { get; set; }
    public int CohortRevisionSnapshot { get; set; }
    public string ImpactSnapshot { get; set; } = "{}";
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
    public List<CohortScheduleRequestEvent> History { get; set; } = [];
}

// Lịch sử bất biến của yêu cầu dời/hủy lớp (append-only).
public sealed class CohortScheduleRequestEvent
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public CohortScheduleRequest Request { get; set; } = null!;
    public string Action { get; set; } = "";
    public int ActorId { get; set; }
    public string ActorRole { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

// Lịch sử thay Mentor chính cho phần còn lại của lớp. Buổi đã diễn ra giữ nguyên Mentor thực dạy;
// không dùng cho Mentor dạy thay một buổi riêng lẻ.
public sealed class StudyCohortMentorAssignment
{
    public int Id { get; set; }
    public int CohortId { get; set; }
    public StudyCohort Cohort { get; set; } = null!;
    public int PreviousMentorId { get; set; }
    public int MentorId { get; set; }
    public int EffectiveFromSessionNumber { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public int AffectedSessionCount { get; set; }
    public string Reason { get; set; } = "";
    public int AssignedById { get; set; }
    public Guid IdempotencyKey { get; set; }
    public int? PolicyId { get; set; }
    public int? PolicyVersionSnapshot { get; set; }
    public string ImpactSnapshot { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
