namespace RikiPath.Domain.Entities;

public sealed class StudyCoursePlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CertificateLevelId { get; set; }
    public CertificateLevel CertificateLevel { get; set; } = null!;
    public int DurationMonths { get; set; }
    public int SessionsPerWeek { get; set; }
    public int? SessionMinutes { get; set; }
    public int? TotalSessions { get; set; }
    public int? RetakeClaimDays { get; set; }
    public decimal Price { get; set; }
    public bool IsPublished { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class StudyCourse
{
    public int Id { get; set; }
    public int LearnerId { get; set; }
    public Guid PurchaseKey { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public int? MentorId { get; set; }
    public UserAccount? Mentor { get; set; }
    public int PlanId { get; set; }
    public StudyCoursePlan Plan { get; set; } = null!;
    public int? RetakeOfId { get; set; }
    public StudyCourse? RetakeOf { get; set; }
    public string Name { get; set; } = "";
    public int CertificateLevelId { get; set; }
    public CertificateLevel CertificateLevel { get; set; } = null!;
    public int PlanRevision { get; set; }
    public int DurationMonths { get; set; }
    public int SessionsPerWeek { get; set; }
    public int SessionMinutes { get; set; }
    public int TotalSessions { get; set; }
    public int? RetakeClaimDays { get; set; }
    public int MinimumHomeworkPercent { get; set; } = 90;
    public int MaximumAbsencePercent { get; set; } = 20;
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string? PaymentReference { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTime? PaymentExpiresAt { get; set; }
    public string Status { get; set; } = "PendingPayment";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<StudyCourseSession> Sessions { get; set; } = [];
    public List<StudyCourseHomework> Homework { get; set; } = [];
    public List<CourseRetakeRequest> RetakeRequests { get; set; } = [];
    public List<CourseAuditEntry> Audit { get; set; } = [];
}

public sealed class StudyCourseSession
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public StudyCourse Course { get; set; } = null!;
    // Logical session number is shared by the original session and its make-up attempts.
    public int Number { get; set; }
    public int Attempt { get; set; } = 1;
    public DateTime ScheduledAt { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? MeetingUrl { get; set; }
    public string? Note { get; set; }
    public int? ConfirmedById { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

public sealed class StudyCourseHomework
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public StudyCourse Course { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public bool Required { get; set; } = true;
    public DateTime DueAt { get; set; }
    public string? Submission { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool? Passed { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Feedback { get; set; }
}

public sealed class CourseRetakeRequest
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public StudyCourse Course { get; set; } = null!;
    public DateTime ExamDate { get; set; }
    public DateTime ResultPublishedAt { get; set; }
    public string EvidenceUrl { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Response { get; set; }
    public string? EligibilitySnapshot { get; set; }
}

public sealed class CourseAuditEntry
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public StudyCourse Course { get; set; } = null!;
    public int ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
