namespace RikiPath.Domain.Entities;

// Đánh giá cố vấn trong khóa học/lớp nhóm; tách biệt hoàn toàn với MentorReview của buổi tư vấn 1-1.
public sealed class CohortMentorReview
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public StudyCourse Course { get; set; } = null!;
    public int? CohortId { get; set; }
    public StudyCohort? Cohort { get; set; }
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsHidden { get; set; }
}

// Khiếu nại khóa học/lớp nhóm kèm minh chứng và lịch sử bất biến; tách biệt với MentorDispute của buổi tư vấn 1-1.
public sealed class CohortComplaint
{
    public const string CategoryTeachingQuality = "TeachingQuality";
    public const string CategoryMentorConduct = "MentorConduct";
    public const string CategorySchedule = "Schedule";
    public const string CategoryPaymentEnrollment = "PaymentEnrollment";
    public const string CategoryOther = "Other";

    public const string Open = "Open";
    public const string UnderReview = "UnderReview";
    public const string Resolved = "Resolved";
    public const string Rejected = "Rejected";

    public int Id { get; set; }
    public int CourseId { get; set; }
    public StudyCourse Course { get; set; } = null!;
    public int? CohortId { get; set; }
    public StudyCohort? Cohort { get; set; }
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public int? MentorId { get; set; }
    public UserAccount? Mentor { get; set; }
    public string Category { get; set; } = CategoryOther;
    public string Message { get; set; } = "";
    public string? EvidenceUrl { get; set; }
    public bool RefundReviewRequested { get; set; }
    public string Status { get; set; } = Open;
    public string? MentorResponse { get; set; }
    public DateTime? MentorRespondedAtUtc { get; set; }
    public string? Decision { get; set; }
    public int? DecidedById { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid IdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
    public List<CohortComplaintEvent> History { get; set; } = [];
}

// Lịch sử bất biến của khiếu nại khóa học (append-only).
public sealed class CohortComplaintEvent
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public CohortComplaint Complaint { get; set; } = null!;
    public string Action { get; set; } = "";
    public int ActorId { get; set; }
    public string ActorRole { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
