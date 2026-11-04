namespace RikiPath.Application.Responses.MentorMeetings
{
    public sealed class MentorMeetingPlanResponse
    {
        public bool IsActive { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
        public int MeetingSessionCount { get; set; }
        public int? CertificateLevelId { get; set; }
        public string? JlptLevel { get; set; }
        public string PackageType => MeetingSessionCount switch
        {
            1 => "Gói trải nghiệm",
            3 => "Gói đồng hành",
            _ => "Gói tư vấn"
        };
        public List<MentorMeetingFeatureResponse> Features { get; set; } = [];
    }
}
