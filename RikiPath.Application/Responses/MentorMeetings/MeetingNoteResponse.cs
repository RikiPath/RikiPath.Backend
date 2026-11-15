namespace RikiPath.Application.Responses.MentorMeetings
{
    public class MeetingNoteResponse
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string? Content { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
