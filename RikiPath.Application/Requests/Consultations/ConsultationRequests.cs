namespace RikiPath.Application.Requests.Consultations
{
    public class PurchasePackageRequest
    {
        public int ConsultationPackageId { get; set; }
        public string ReturnUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class BookMentorBooking
    {
        public int UserSubscriptionId { get; set; }
        public int MentorAvailabilityId { get; set; }
        public string Topic { get; set; } = string.Empty;
    }

    public class SubmitTicketRequest
    {
        public int UserSubscriptionId { get; set; }
        public string Question { get; set; }
    }

    public class AnswerTicketRequest
    {
        public string AnswerText { get; set; } = string.Empty;
    }

    public class BookMeetingRequest
    {
        public int UserSubscriptionId { get; set; }
        public int MentorAvailabilityId { get; set; }
        public string? Note { get; set; }
    }

    public class SubmitAnswerRequest
    {
        public string AnswerText { get; set; } = string.Empty;
    }

    public class LogMeetingNoteRequest
    {
        public string MeetingNotes { get; set; } = string.Empty;
    }
}
