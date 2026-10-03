namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorMeetingPlanResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
        public int MeetingSessionCount { get; set; }
        public List<MentorMeetingFeatureResponse> Features { get; set; } = [];
    }
}
