using RikiPath.Application.Helpers;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Reviews
{
    public enum ReviewContentType
    {
        Vocabulary,
        Kanji,
        Grammar,
        Custom
    }

    public sealed class SrsIntervalPreviewResponse
    {
        public int AgainDays { get; set; }
        public int HardDays { get; set; }
        public int GoodDays { get; set; }
        public int EasyDays { get; set; }
        public string AgainLabel { get; set; } = "1 ngày";
        public string HardLabel { get; set; } = "1 ngày";
        public string GoodLabel { get; set; } = "1 ngày";
        public string EasyLabel { get; set; } = "4 ngày";
    }

    public sealed class SrsMcqOptionResponse
    {
        public string OptionKey { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? SubText { get; set; }
    }

    public class ReviewQueueItemResponse
    {
        public int ReviewCardId { get; set; }
        public ReviewContentType ContentType { get; set; }
        public string CardType { get; set; } = "Vocabulary";
        public string Term { get; set; } = string.Empty;      // Front / Kanji / Word / Grammar title
        public string? Reading { get; set; }                  // Hiragana / On-Kun / Grammar structure
        public string Meaning { get; set; } = string.Empty;
        public string BackText { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ExampleMeaning { get; set; }
        public string? ContextSnippet { get; set; }
        public string? Note { get; set; }                     // Personal note
        public string? JlptLevel { get; set; }
        public string? AudioUrl { get; set; }

        public SrsCardStatus Status { get; set; } = SrsCardStatus.Active;
        public SrsLearningState LearningState { get; set; } = SrsLearningState.New;
        public string SourceType { get; set; } = "Manual";
        public int? SourceId { get; set; }
        public string? SourceLabel { get; set; }
        public int? LessonId { get; set; }
        public int? VocabularyId { get; set; }
        public int? KanjiId { get; set; }
        public int? GrammarPatternId { get; set; }
        public int? LearnerNoteEntryId { get; set; }
        public int? SrsCardTemplateId { get; set; }
        public bool SourceUnpublished { get; set; }

        public List<SrsSourceLinkItem> SourceLinks { get; set; } = [];
        public List<string> AcceptedReadings { get; set; } = [];
        public List<string> Synonyms { get; set; } = [];

        public double EaseFactor { get; set; } = 2.5;
        public int IntervalDays { get; set; }
        public int Repetitions { get; set; }
        public int Lapses { get; set; }
        public int CorrectCount { get; set; }
        public int IncorrectCount { get; set; }
        public DateTime NextReviewDate { get; set; }
        public DateTime? LastReviewedAt { get; set; }
        public DateTime? CreatedAt { get; set; }

        public SrsIntervalPreviewResponse? IntervalPreviews { get; set; }
        public List<SrsMcqOptionResponse> McqOptions { get; set; } = [];
        public string AssignedStudyMode { get; set; } = "flip";
        public List<string> EligibleModes { get; set; } = ["flip"];
        public string TypingPromptKind { get; set; } = "reading_or_meaning";
    }
}
