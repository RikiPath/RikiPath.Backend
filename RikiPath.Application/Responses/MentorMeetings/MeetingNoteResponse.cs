namespace RikiPath.Application.Responses.MentorMeetings
{
    public class MeetingNoteResponse
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>"Mentor" | "Learner".</summary>
        public string Role { get; set; } = string.Empty;

        public string? Content { get; set; }

        /// <summary>UTC. FE tự format theo múi giờ của mình.</summary>
        public DateTime UpdatedAt { get; set; }
    }
}
