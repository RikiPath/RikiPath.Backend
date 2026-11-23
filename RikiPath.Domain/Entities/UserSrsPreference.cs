namespace RikiPath.Domain.Entities;

/// <summary>Per-learner SRS study, auto-enroll, and daily reminder notification preferences.</summary>
public class UserSrsPreference : Base
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;

    public bool AutoEnrollAfterLesson { get; set; }
    public int DailyNewLimit { get; set; } = 20;
    public int DailyReviewLimit { get; set; } = 100;
    public string PreferredReviewMode { get; set; } = "flip";
    public bool AutoPlayAudio { get; set; } = true;
    public bool ShowFuriganaHint { get; set; } = true;

    public bool DailyReminderEnabled { get; set; } = true;
    public int ReminderHour { get; set; } = 20;
    public int ReminderMinute { get; set; }
    public string ReminderFrequency { get; set; } = "Daily";
    public string? ReminderDaysJson { get; set; }
    public bool EmailReminderEnabled { get; set; }
    public string TimezoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public string PreferredLocale { get; set; } = "vi";

    public DateTime? LastReminderSentAtUtc { get; set; }
    public DateOnly? LastReminderSentLocalDate { get; set; }
}
