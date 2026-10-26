namespace RikiPath.Application.Requests.MentorMeetings
{
    public sealed class CancelAvailabilitiesRequest
    {
        public List<int> Ids { get; set; } = [];
        public string? Reason { get; set; }
    }
}
