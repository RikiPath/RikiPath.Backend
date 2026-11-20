namespace RikiPath.Domain.Entities;

/// <summary>An assignment from a mentor to a learner, associated with zero or more lessons.</summary>
public class HomeworkAssignment : Base
{
    public int Id { get; set; }
    public int LearnerId { get; set; }
    public UserAccount Learner { get; set; } = null!;
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public int? MentorBookingId { get; set; }
    public MentorBooking? MentorBooking { get; set; }
    public int? CohortId { get; set; }
    public StudyCohort? Cohort { get; set; }
    public int? StudyCourseId { get; set; }
    public StudyCourse? StudyCourse { get; set; }
    public Guid? CohortAssignmentGroupKey { get; set; }
    public string AssignmentScope { get; set; } = "Booking1on1";
    public List<HomeworkAssignmentLesson> HomeworkAssignmentLessons { get; set; } = new();
    public List<HomeworkAssignmentExercise> Exercises { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? AuthoredDefinitionJson { get; set; }
    public DateTime? DueAt { get; set; }
    public string Status { get; set; } = "Assigned";
    public string? AiDraftFeedback { get; set; }
    public double? AiDraftScore { get; set; }
    public DateTime? AiDraftGeneratedAtUtc { get; set; }
    public string? AiDraftUnavailableReason { get; set; }
    public string? MentorFeedback { get; set; }
    public double? MentorScore { get; set; }
    public bool? Passed { get; set; }
    public DateTime? PublishedFeedbackAtUtc { get; set; }
    public int? PublishedByMentorId { get; set; }
    public HomeworkSubmission? Submission { get; set; }
}
