using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorProfile
{
    public class AdminMentorProfileResponse
    {
        public int MentorId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }
        public string? Bio { get; set; }
        public string? Expertise { get; set; }
        public string? JlptLevel { get; set; }
        public int? YearsOfExperience { get; set; }
        public string? Workplace { get; set; }
        public bool HasVerificationDocument { get; set; }
        public string? IntroductionVideoUrl { get; set; }
        public MentorVerificationStatus VerificationStatus { get; set; }
        public string VerificationStatusDescription { get; set; } = string.Empty;
        public bool IsBookable { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByAdminName { get; set; }
        public string? RejectionReason { get; set; }
        public string? ReviewNotes { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
