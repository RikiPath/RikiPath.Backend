namespace RikiPath.Application.Responses.Profile
{
    public class UserProfileResponse
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }

        public int? TargetJlptLevelId { get; set; }
        public string? TargetJlptLevelName { get; set; }
        public int DailyStudyMinutes { get; set; }

        public bool EmailNotificationsEnabled { get; set; }
        public bool SystemNotificationsEnabled { get; set; }
    }
}
