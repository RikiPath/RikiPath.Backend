namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class CreateMentorAvailabilityRequest
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateOnly? RepeatUntil { get; set; }
        public List<DayOfWeek>? Weekdays { get; set; }
        /// <summary>Khóa idempotency tùy chọn để chống tạo trùng khi double-click hoặc retry mạng.</summary>
        public string? IdempotencyKey { get; set; }
    }

    public sealed class MentorAvailabilityBatchSlotItem
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public sealed class CreateMentorAvailabilityBatchRequest
    {
        public List<MentorAvailabilityBatchSlotItem> Slots { get; set; } = [];
        /// <summary>Khóa idempotency tùy chọn để chống tạo trùng khi double-click hoặc retry mạng.</summary>
        public string? IdempotencyKey { get; set; }
    }
}
