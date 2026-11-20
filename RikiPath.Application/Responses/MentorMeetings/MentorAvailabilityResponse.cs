namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorAvailabilityResponse
    {
        public int Id { get; set; }
        public int MentorId { get; set; }
        public string MentorName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsBooked { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }
        public int BookedCount { get; set; }
        public int MaxLearners { get; set; }
        public string? AdminName { get; set; }
        public int AdvanceDaysAtCreation { get; set; }
        public bool MeetsAdvanceRule { get; set; }
        public DateTime? CancelRequestedAt { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? CancelRejectedAt { get; set; }
        public string? CancelRejectionReason { get; set; }
        public int? MentorWeeklyAvailabilityId { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? ChangeRequestReason { get; set; }
        public DateTime? ChangeRequestedAt { get; set; }
        public string? ChangeResolution { get; set; }
        public bool IsInCohort { get; set; }
        public int? CohortId { get; set; }
        public string? CohortName { get; set; }
        public string? CohortStatus { get; set; }
        public int? SessionNumber { get; set; }
        public bool SessionCompleted { get; set; }
        public int AttendanceConfirmedCount { get; set; }
        public bool IsDeleted { get; set; }
        public string Status { get; set; } = string.Empty;
        public string EventType { get; set; } = "Availability";
        public DateTime? CreatedAt { get; set; }
        /// <summary>Mã nhóm yêu cầu đăng ký cùng đợt để Admin dễ lọc và duyệt hàng loạt.</summary>
        public string? BatchGroupKey { get; set; }
    }

    public sealed class MentorAvailabilityQueryRequest
    {
        public string? Status { get; set; }
        public string? EventType { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool IncludeDeleted { get; set; } = true;
    }

    public sealed class PagedMentorAvailabilityResponse
    {
        public List<MentorAvailabilityResponse> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
