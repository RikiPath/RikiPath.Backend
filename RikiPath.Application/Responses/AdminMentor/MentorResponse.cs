namespace RikiPath.Application.Responses.AdminMentor
{
    public class MentorResponse
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsActive { get; set; } = true;
        public string VerificationStatus { get; set; } = string.Empty;
        public string? JlptLevel { get; set; }
        public IReadOnlyList<string> TeachingLevels { get; set; } = [];
        public DateTime? CreatedDate { get; set; }
    }
}
