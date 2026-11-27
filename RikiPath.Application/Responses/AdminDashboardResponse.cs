namespace RikiPath.Application.Responses;

public sealed class AdminDashboardDailyRevenuePoint
{
    public string Date { get; init; } = string.Empty;
    public decimal SubscriptionAmount { get; init; }
    public decimal CourseTuitionAmount { get; init; }
    public decimal AiTopUpAmount { get; init; }
    public decimal RefundedAmount { get; init; }
    public decimal TotalPaidAmount { get; init; }
}

public sealed class AdminDashboardLevelTuitionItem
{
    public string LevelCode { get; init; } = string.Empty;
    public int EnrolledCount { get; init; }
    public decimal TotalTuitionAmount { get; init; }
}

public sealed class AdminDashboardRevenueSummary
{
    public decimal TotalPaidAmount { get; init; }
    public decimal SubscriptionRevenue { get; init; }
    public decimal CourseTuitionRevenue { get; init; }
    public decimal AiTopUpRevenue { get; init; }
    public decimal TotalRefundedAmount { get; init; }
    public decimal NetCollectedAmount { get; init; }
    public int PaidCoursesCount { get; init; }
    public int PendingPaymentCount { get; init; }
    public int ReviewPaymentCount { get; init; }
    public List<AdminDashboardDailyRevenuePoint> DailySeries { get; init; } = [];
    public List<AdminDashboardLevelTuitionItem> CourseTuitionByLevel { get; init; } = [];
}

public sealed class AdminDashboardResponse
{
    public int TotalUsers { get; init; }
    public int Learners { get; init; }
    public int Mentors { get; init; }
    public int PendingContent { get; init; }
    public int PendingMentorSchedules { get; init; }
    public int PendingTeachingProposals { get; init; }
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
    public AdminDashboardRevenueSummary RevenueSummary { get; init; } = new();
}
