using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class MentorBooking : Base
    {
        public int Id { get; set; }
        public uint Version { get; set; }

        public int UserSubscriptionId { get; set; }
        public UserSubscription UserSubscription { get; set; } = null!;

        public int? MentorAvailabilityId { get; set; }
        public MentorAvailability? MentorAvailability { get; set; }

        public MentorStatus Status { get; set; } = MentorStatus.PendingAssignment;
        public string? Question { get; set; }
        public DateTime? ScheduledAt { get; set; }

        public string? MeetingLink { get; set; }
        public DateTime? CompletedAt { get; set; }

        public Note? Note { get; set; }

        // Siêu dữ liệu phục vụ hủy lịch, dời lịch và xử lý ngoại lệ hoàn vé (Stage 10/13)
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public int? CancelledByUserId { get; set; }
        public string? CancelledByRole { get; set; }
        public bool ExceptionRequested { get; set; }
        public string? ExceptionReason { get; set; }
        public bool? ExceptionApproved { get; set; }
    }
}
