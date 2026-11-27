namespace RikiPath.Application.Responses.MentorProfile
{
    public class PublicMentorProfileResponse
    {
        public int MentorId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
        public string? Expertise { get; set; }
        public string? JlptLevel { get; set; }
        public List<string> TeachingJlptLevels { get; set; } = [];
        public int? YearsOfExperience { get; set; }
        public string? Workplace { get; set; }
        public string? IntroductionVideoUrl { get; set; }
        public bool IsBookable { get; set; }
    }
}
