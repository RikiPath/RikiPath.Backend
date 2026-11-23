using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Profile
{
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "Họ và tên không được để trống."), MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        /// <summary>True when caller explicitly edits or clears PhoneNumber; prevents partial callers from wiping it.</summary>
        public bool UpdatePhoneNumber { get; set; }

        [MaxLength(50)]
        public string? StudyTimePreference { get; set; }
    }

    public class SetJlptGoalRequest
    {
        public int TargetJlptLevelId { get; set; }
        public int? TargetCertificateLevelId { get; set; }
        public int? CertificateTypeId { get; set; }
        public string? DeclaredJlptCertificateCode { get; set; }

        /// <summary>Số phút học mong muốn mỗi ngày, dùng để tính streak/nhắc học.</summary>
        public int? DailyStudyMinutesMin { get; set; }
        public int? DailyStudyMinutesMax { get; set; }
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

    public class SetRoadmapModeRequest
    {
        [Required]
        public string Mode { get; set; } = "STANDARD";
    }

    public class SetLearningGoalRequest
    {
        public int CurrentStudyLevelId { get; set; }
        public int TargetJlptLevelId { get; set; }
        public int DailyStudyMinutes { get; set; }
        /// <summary>Complete-form semantics: null explicitly clears the planned date.</summary>
        public DateTime? TargetExamDate { get; set; }
        public bool HasJlptCertificate { get; set; }
        public int? CertifiedJlptLevelId { get; set; }
        public int ExpectedVersion { get; set; }
    }

    public class UpdateNotificationSettingsRequest
    {
        public bool EmailNotificationsEnabled { get; set; }
        public bool SystemNotificationsEnabled { get; set; }
        public Dictionary<string, bool>? NotificationCategories { get; set; }
    }
}
