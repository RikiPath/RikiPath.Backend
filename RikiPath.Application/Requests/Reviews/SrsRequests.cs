using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Reviews;

public class EnrollSrsCardRequest
{
    public int? VocabularyId { get; set; }
    public int? KanjiId { get; set; }
    public int? GrammarPatternId { get; set; }
    public int? LearnerNoteEntryId { get; set; }
    public int? SrsCardTemplateId { get; set; }
    public int? LessonId { get; set; }

    [MaxLength(40)]
    public string? SourceType { get; set; }

    public int? SourceId { get; set; }

    [MaxLength(300)]
    public string? SourceLabel { get; set; }

    [MaxLength(30)]
    public string? CardType { get; set; }

    [MaxLength(500)]
    public string? FrontText { get; set; }

    [MaxLength(1000)]
    public string? BackText { get; set; }

    [MaxLength(300)]
    public string? Reading { get; set; }

    [MaxLength(500)]
    public string? Meaning { get; set; }

    [MaxLength(1000)]
    public string? ExampleSentence { get; set; }

    [MaxLength(1000)]
    public string? ExampleMeaning { get; set; }

    [MaxLength(1000)]
    public string? ContextSnippet { get; set; }

    [MaxLength(10)]
    public string? JlptLevel { get; set; }

    [MaxLength(1000)]
    public string? AudioUrl { get; set; }

    [MaxLength(1000)]
    public string? PersonalNote { get; set; }

    public List<string>? AcceptedReadings { get; set; }
    public List<string>? Synonyms { get; set; }
    public bool ReactivateIfArchived { get; set; } = true;
}

public class BatchEnrollSrsCardsRequest
{
    [Required]
    public List<EnrollSrsCardRequest> Cards { get; set; } = [];
}

public class UpdateSrsCardRequest
{
    [MaxLength(30)]
    public string? CardType { get; set; }

    [MaxLength(500)]
    public string? FrontText { get; set; }

    [MaxLength(1000)]
    public string? BackText { get; set; }

    [MaxLength(300)]
    public string? Reading { get; set; }

    [MaxLength(500)]
    public string? Meaning { get; set; }

    [MaxLength(1000)]
    public string? ExampleSentence { get; set; }

    [MaxLength(1000)]
    public string? ExampleMeaning { get; set; }

    [MaxLength(1000)]
    public string? ContextSnippet { get; set; }

    [MaxLength(10)]
    public string? JlptLevel { get; set; }

    [MaxLength(1000)]
    public string? AudioUrl { get; set; }

    [MaxLength(1000)]
    public string? PersonalNote { get; set; }

    public List<string>? AcceptedReadings { get; set; }
    public List<string>? Synonyms { get; set; }
}

public class UpdateUserSrsPreferenceRequest
{
    public bool AutoEnrollAfterLesson { get; set; }

    [Range(0, 200, ErrorMessage = "Thẻ mới mỗi ngày phải từ 0 đến 200.")]
    public int DailyNewLimit { get; set; } = 20;

    [Range(5, 500, ErrorMessage = "Mục tiêu ôn mỗi ngày phải từ 5 đến 500.")]
    public int DailyReviewLimit { get; set; } = 100;

    [MaxLength(20)]
    public string PreferredReviewMode { get; set; } = "flip";

    public bool AutoPlayAudio { get; set; } = true;
    public bool ShowFuriganaHint { get; set; } = true;

    public bool DailyReminderEnabled { get; set; } = true;

    [Range(0, 23)]
    public int ReminderHour { get; set; } = 20;

    [Range(0, 59)]
    public int ReminderMinute { get; set; }

    [MaxLength(30)]
    public string ReminderFrequency { get; set; } = "Daily";

    public List<int>? ReminderDays { get; set; }
    public bool EmailReminderEnabled { get; set; }

    [MaxLength(100)]
    public string TimezoneId { get; set; } = "Asia/Ho_Chi_Minh";

    [MaxLength(10)]
    public string PreferredLocale { get; set; } = "vi";

    public DateTime? ExpectedUpdatedAtUtc { get; set; }
}

public class StartSrsSessionRequest
{
    [MaxLength(20)]
    public string StudyMode { get; set; } = "flip";

    [MaxLength(20)]
    public string SessionKind { get; set; } = "Scheduled"; // Scheduled | Practice

    [Range(1, 100)]
    public int MaxCards { get; set; } = 25;

    [MaxLength(30)]
    public string? CardType { get; set; }

    [MaxLength(10)]
    public string? JlptLevel { get; set; }

    [MaxLength(40)]
    public string? SourceType { get; set; }

    [MaxLength(20)]
    public string? LearningState { get; set; }

    [MaxLength(120)]
    public string? Search { get; set; }

    [MaxLength(30)]
    public string? SelectionScope { get; set; } // all | selected | filtered

    public List<int>? CardIds { get; set; }

    public bool Shuffle { get; set; }

    public int? TimeLimitMinutes { get; set; }
}
