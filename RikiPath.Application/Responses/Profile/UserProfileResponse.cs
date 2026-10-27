using System.Text.Json.Serialization;

namespace RikiPath.Application.Responses.Profile
{
    public class UserProfileResponse
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? AvatarUrl { get; set; }

        public string Role { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? TargetJlptLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? TargetJlptLevelName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? StartingJlptLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? StartingJlptLevelName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? CurrentStudyLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? CurrentStudyLevelName { get; set; }

        public string LearningMode { get; set; } = "STANDARD";

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? AssessedJlptLevelId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? AssessedJlptLevelName { get; set; }

        public string PlacementDecision { get; set; } = "NOT_TAKEN";

        public int DailyStudyMinutes { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DateTime? TargetExamDate { get; set; }

        public bool EmailNotificationsEnabled { get; set; }
        public bool SystemNotificationsEnabled { get; set; }
    }
}
