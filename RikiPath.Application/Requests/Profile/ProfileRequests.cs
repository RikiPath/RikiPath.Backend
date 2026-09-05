namespace RikiPath.Application.Requests.Profile
{
    public class UpdateProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    public class SetJlptGoalRequest
    {
        public int TargetJlptLevelId { get; set; }

        /// <summary>Số phút học mong muốn mỗi ngày, dùng để tính streak/nhắc học.</summary>
        public int DailyStudyMinutes { get; set; }
    }

    public class UpdateNotificationSettingsRequest
    {
        public bool EmailNotificationsEnabled { get; set; }
        public bool SystemNotificationsEnabled { get; set; }
    }
}
