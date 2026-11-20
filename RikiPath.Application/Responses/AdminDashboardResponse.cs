namespace RikiPath.Application.Responses;

public sealed class AdminDashboardResponse
{
    public int TotalUsers { get; init; }
    public int Learners { get; init; }
    public int Mentors { get; init; }
    public int PendingContent { get; init; }
    public int PendingMentorSchedules { get; init; }
    public int PendingVerifications { get; init; }
    public int PendingSlotCancelRequests { get; init; }
    public int PendingSlotChangeRequests { get; init; }
    public int OpenComplaints { get; init; }
    public int PendingTransferRequests { get; init; }
    public int PendingCohortScheduleRequests { get; init; }
    public int PublishedCohorts { get; init; }
    public int DraftCohorts { get; init; }
    public int PaymentReviewCourses { get; init; }
    public int MentorBookings { get; init; }
    public int ActivePlans { get; init; }
    public int PaidSubscriptions { get; init; }
}
