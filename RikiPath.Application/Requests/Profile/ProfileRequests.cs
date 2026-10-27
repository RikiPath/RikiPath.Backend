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

        /// <summary>Ngày dự định thi, dùng để tạo lộ trình học. Phải sau ngày hiện tại.</summary>
        public DateTime? TargetExamDate { get; set; }
        /// <summary>Only true explicitly clears an existing date; omitted/null preserves it.</summary>
        public bool ClearTargetExamDate { get; set; }
    }

    public class SetStudyLevelRequest
    {
        public int CurrentStudyLevelId { get; set; }
        public bool Confirmed { get; set; }
    }

    public class UpdateNotificationSettingsRequest
    {
        public bool EmailNotificationsEnabled { get; set; }
        public bool SystemNotificationsEnabled { get; set; }
    }
}
