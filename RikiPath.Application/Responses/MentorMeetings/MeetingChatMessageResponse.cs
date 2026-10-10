namespace RikiPath.Application.Responses.MentorMeetings
{
    public class MeetingChatMessageResponse
    {
        public string Id { get; set; } = string.Empty;

        public int SenderUserId { get; set; }

        public string Sender { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }
    }
}
