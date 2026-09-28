using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Consultations
{
    public class CreatePurchaseResponse
    {
        public int PurchaseId { get; set; }
        public long OrderCode { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public class BookingResponse
    {
        public int MentorBookingId { get; set; }
        public int MentorAvailabilityId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AvailableDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    public class SubmitTicketResponse
    {
        public int MentorBookingId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TicketQueueItem
    {
        public int MentorBookingId { get; set; }
        public int UserId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
    }

    public class AnswerTicketResponse
    {
        public int MentorBookingId { get; set; }
        public string AnswerText { get; set; } = string.Empty;
        public DateTime AnsweredAt { get; set; }
    }

    public class MockTestSummary
    {
        public string TestTitle { get; set; } = string.Empty;
        public double? Score { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    public class MentorBookingResponse
    {
        public int Id { get; set; }
        public ConsultationType Type { get; set; }
        public ConsultationStatus Status { get; set; }
        public int? MentorId { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string? MeetingLink { get; set; }
        public string? Question { get; set; }
        public string? AnswerText { get; set; }
        public string? MeetingNotes { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class MentorQueueItem
    {
        public int RequestId { get; set; }
        public int LearnerId { get; set; }
        public string LearnerName { get; set; } = string.Empty;
        public ConsultationType Type { get; set; }
        public ConsultationStatus Status { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string? Question { get; set; }
        public List<MockTestSummary> RecentMockTestResults { get; set; } = new();
    }

    public class MentorQueueResponse
    {
        public List<MentorQueueItem> Items { get; set; } = new();
    }
}
