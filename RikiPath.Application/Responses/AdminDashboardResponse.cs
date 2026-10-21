namespace RikiPath.Application.Responses;

public sealed class AdminDashboardResponse
{
    public int TotalUsers { get; init; }
    public int Learners { get; init; }
    public int Mentors { get; init; }
    public int PendingContent { get; init; }
    public int PendingMentorSchedules { get; init; }
    public int MentorBookings { get; init; }
    public int ActivePlans { get; init; }
    public int PaidSubscriptions { get; init; }
}
