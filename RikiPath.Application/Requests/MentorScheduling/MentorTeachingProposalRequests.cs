using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.MentorScheduling;

public sealed class WeeklySessionSlotDto
{
    /// <summary>0 = Chủ nhật, 1 = Thứ 2 .. 6 = Thứ 7 (theo giờ Việt Nam Asia/Ho_Chi_Minh).</summary>
    [Range(0, 6)]
    public int Weekday { get; set; }

    /// <summary>Phút kể từ 00:00 giờ Việt Nam (VD: 480 = 08:00, 1140 = 19:00).</summary>
    [Range(0, 1439)]
    public int StartMinute { get; set; }

    /// <summary>Phút kết thúc kể từ 00:00 giờ Việt Nam (phải bằng StartMinute + SessionDurationMinutes).</summary>
    [Range(1, 1440)]
    public int EndMinute { get; set; }
}

public sealed class ProposalAttachmentDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(260)]
    public string? StoredFileName { get; set; }

    [MaxLength(120)]
    public string? ContentType { get; set; }

    [Range(0, 50 * 1024 * 1024)]
    public long? SizeBytes { get; set; }
}

public sealed class SaveTeachingProposalDraftRequest
{
    public int? CertificateLevelId { get; set; }

    [MaxLength(32)]
    public string? ProgramType { get; set; }

    public int? CoursePlanId { get; set; }

    [MaxLength(200)]
    public string? CourseTitle { get; set; }

    [MaxLength(1500)]
    public string? CourseObjective { get; set; }

    public DateOnly? ExpectedStartDate { get; set; }

    [Range(1, 52)]
    public int? CourseDurationWeeks { get; set; }

    [Range(30, 240)]
    public int? SessionDurationMinutes { get; set; }

    public List<WeeklySessionSlotDto>? WeeklySessions { get; set; }

    [MaxLength(2000)]
    public string? OptionalNotes { get; set; }

    /// <summary>Tệp đính kèm không bắt buộc (giáo trình/đề cương tham khảo).</summary>
    public List<ProposalAttachmentDto>? OptionalAttachments { get; set; }

    /// <summary>Bắt buộc khi cập nhật bản nháp đã tồn tại để kiểm soát ghi đè đồng thời (optimistic concurrency).</summary>
    public int? ExpectedRevision { get; set; }
}

public sealed class SubmitTeachingProposalRequest
{
    /// <summary>Khóa chống gửi trùng (idempotency) tạo từ phía client cho mỗi lần bấm gửi duyệt.</summary>
    [Required]
    [MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Phiên bản bản nháp hiện tại nhằm đảm bảo không gửi dữ liệu cũ.</summary>
    [Range(1, int.MaxValue)]
    public int ExpectedRevision { get; set; }
}

public sealed class ValidateTeachingProposalRequest
{
    public int? ProposalId { get; set; }
    public int? CertificateLevelId { get; set; }
    public string? ProgramType { get; set; }
    public int? CoursePlanId { get; set; }
    public string? CourseObjective { get; set; }
    public DateOnly? ExpectedStartDate { get; set; }
    public int? CourseDurationWeeks { get; set; }
    public int? SessionDurationMinutes { get; set; }
    public List<WeeklySessionSlotDto> WeeklySessions { get; set; } = [];
}

public sealed class WithdrawTeachingProposalRequest
{
    [MaxLength(1000)]
    public string? Reason { get; set; }

    public int? ExpectedRevision { get; set; }
}

public sealed class ReviewTeachingProposalRequest
{
    /// <summary>Approve | RequestChanges | Reject</summary>
    [Required]
    [MaxLength(32)]
    public string Decision { get; set; } = string.Empty;

    [MaxLength(1500)]
    public string? Reason { get; set; }

    [Range(1, int.MaxValue)]
    public int ExpectedRevision { get; set; }
}
