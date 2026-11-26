namespace RikiPath.Domain.Entities;

// The timetable belongs to the class; StudyCourse remains each learner's paid enrollment.
public sealed class StudyCohort
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public StudyCoursePlan Plan { get; set; } = null!;
    public string Name { get; set; } = "";
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int Capacity { get; set; }
    public int? CapacitySnapshot { get; set; }
    public int? GroupPolicyId { get; set; }
    public MentorGroupPolicy? GroupPolicy { get; set; }
    public int? PolicyVersionSnapshot { get; set; }
    public int? SourceProposalId { get; set; }
    public MentorTeachingProposal? SourceProposal { get; set; }
    // Price snapshot chốt tại thời điểm phê duyệt/công bố lớp — không bị thay đổi khi bảng giá mới được ban hành.
    public decimal? TuitionPerSessionSnapshot { get; set; }
    public decimal? TotalTuitionSnapshot { get; set; }
    public int? TuitionPolicyVersionSnapshot { get; set; }
    public DateTime? TuitionSnapshotAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Draft";
    public int Revision { get; set; } = 1;
    public List<StudyCohortSession> Sessions { get; set; } = [];
    public List<StudyCourse> Enrollments { get; set; } = [];
}

public sealed class StudyCohortSession
{
    public int Id { get; set; }
    public int CohortId { get; set; }
    public StudyCohort Cohort { get; set; } = null!;
    public int Number { get; set; }
    public int AvailabilityId { get; set; }
    public MentorAvailability Availability { get; set; } = null!;
}
