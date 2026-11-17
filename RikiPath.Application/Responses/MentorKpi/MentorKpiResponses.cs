namespace RikiPath.Application.Responses.MentorKpi;

public sealed class MentorKpiPeriodItemResponse
{
    public string PeriodKey { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int TotalAssignedSessions { get; set; }
    public int CompletedValidSessions { get; set; }
    public int ConfirmedTeachingMinutes { get; set; }
    public int ScheduledCompletedMinutes { get; set; }
    public int CancelledSessionsCount { get; set; }
    public int MissedOrNoShowSessionsCount { get; set; }
    public int UpcomingOrIncompleteSessionsCount { get; set; }
    public int HomeworkAssignedCount { get; set; }
    public int HomeworkSubmittedCount { get; set; }
    public int HomeworkGradedCount { get; set; }
    public double? AverageFeedbackTurnaroundMinutes { get; set; }
    public int FeedbackTurnaroundSampleCount { get; set; }
    public int ReviewsCount { get; set; }
    public double? AverageRating { get; set; }
    public int DisputesCount { get; set; }
}

public sealed class MentorKpiRecentSessionItemResponse
{
    public string SourceType { get; set; } = string.Empty; // "Booking1on1" | "StudyCourse"
    public int BookingOrSessionId { get; set; }
    public Guid? RoomId { get; set; }
    public string? LearnerName { get; set; }
    public string? JlptLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledStartUtc { get; set; }
    public DateTime ScheduledEndUtc { get; set; }
    public int ScheduledMinutes { get; set; }
    public int ObservedMentorMinutes { get; set; }
    public DateTime? AttendedAtUtc { get; set; }
    public bool IsValidCompleted { get; set; }
}

public sealed class MentorKpiSummaryResponse
{
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public List<string> TeachingJlptLevels { get; set; } = [];

    // 1. Tổng số buổi được phân công
    public int TotalAssignedSessions { get; set; }
    public int AssignedBookingsCount { get; set; }
    public int AssignedCourseSessionsCount { get; set; }

    // 2. Tổng số buổi hoàn thành hợp lệ
    public int CompletedValidSessions { get; set; }

    // 3. Tổng thời lượng giảng dạy được xác nhận
    public int ConfirmedTeachingMinutes { get; set; }
    public int ScheduledCompletedMinutes { get; set; }

    // 4. Số buổi hủy, vắng mặt hoặc chưa hoàn thành
    public int CancelledSessionsCount { get; set; }
    public int CancelledByMentorCount { get; set; }
    public int CancelledByLearnerCount { get; set; }
    public int MissedOrNoShowSessionsCount { get; set; }
    public int UpcomingOrIncompleteSessionsCount { get; set; }

    // 5. Số bài tập đã giao và đã chấm
    public int HomeworkAssignedCount { get; set; }
    public int HomeworkSubmittedCount { get; set; }
    public int HomeworkGradedCount { get; set; }
    public int BookingHomeworkAssignedCount { get; set; }
    public int BookingHomeworkSubmittedCount { get; set; }
    public int BookingHomeworkGradedCount { get; set; }
    public int CourseHomeworkAssignedCount { get; set; }
    public int CourseHomeworkSubmittedCount { get; set; }
    public int CourseHomeworkGradedCount { get; set; }

    // 6. Thời gian phản hồi bài tập (chỉ trả khi có dữ liệu hợp lệ)
    public double? AverageFeedbackTurnaroundMinutes { get; set; }
    public int FeedbackTurnaroundSampleCount { get; set; }

    // 7. Đánh giá từ học viên & khiếu nại
    public double? AverageRating { get; set; }
    public int TotalReviewsCount { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    public int TotalDisputesCount { get; set; }
    public int OpenDisputesCount { get; set; }
    public int ResolvedRefundCount { get; set; }

    // 8. Thống kê theo tuần và theo tháng
    public List<MentorKpiPeriodItemResponse> WeeklyBreakdown { get; set; } = [];
    public List<MentorKpiPeriodItemResponse> MonthlyBreakdown { get; set; } = [];
    public List<MentorKpiRecentSessionItemResponse> RecentSessions { get; set; } = [];

    // Truy xuất nguồn dữ liệu và quy tắc tính (không trọng số, không lương)
    public Dictionary<string, string> MetricSources { get; set; } = new();
}

public sealed class MentorAttendanceParticipantDto
{
    public int UserId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public int ObservedDurationSeconds { get; set; }
    public int ObservedDurationMinutes { get; set; }
    public int ReconnectionCount { get; set; }
    public DateTime? FirstJoinedAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }
}

public sealed class MentorBookingAttendanceDetailResponse
{
    public int BookingId { get; set; }
    public Guid? RoomId { get; set; }
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public int LearnerId { get; set; }
    public string LearnerName { get; set; } = string.Empty;
    public string? JlptLevel { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public DateTime? ScheduledStartUtc { get; set; }
    public DateTime? ScheduledEndUtc { get; set; }
    public int ScheduledMinutes { get; set; }
    public DateTime? AttendedAtUtc { get; set; }
    public int MentorObservedSeconds { get; set; }
    public int LearnerObservedSeconds { get; set; }
    public bool BothJoinedAtLeastOneMinute { get; set; }
    public List<MentorAttendanceParticipantDto> Participants { get; set; } = [];
}
