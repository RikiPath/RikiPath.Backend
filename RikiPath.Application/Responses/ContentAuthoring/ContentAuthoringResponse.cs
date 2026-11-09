using RikiPath.Application.Requests.ContentAuthoring;

namespace RikiPath.Application.Responses.ContentAuthoring
{
    // ============================ Kanji ============================

    public class KanjiSuggestionItem
    {
        public string Character { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? SinoVietnamese { get; set; }
        public string? OnYomi { get; set; }
        public string? KunYomi { get; set; }
        public int StrokeCount { get; set; }
        public string? Rationale { get; set; }
        public bool IsDuplicate { get; set; }
        public int? ExistingKanjiId { get; set; }
    }

    public class SuggestKanjiResponse
    {
        public List<KanjiSuggestionItem> Items { get; set; } = new();
    }

    // ============================ Vocabulary ============================

    public class VocabularySuggestionItem
    {
        public string Word { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }
        public string? Rationale { get; set; }
        public bool IsDuplicate { get; set; }
        public int? ExistingVocabularyId { get; set; }
    }

    public class SuggestVocabularyResponse
    {
        public List<VocabularySuggestionItem> Items { get; set; } = new();
    }

    // ============================ Grammar ============================

    public class GrammarSuggestionItem
    {
        public string Title { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
        public string? UsageNotes { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }
        public string? Rationale { get; set; }
        public bool IsDuplicate { get; set; }
        public int? ExistingGrammarPatternId { get; set; }
    }

    public class SuggestGrammarResponse
    {
        public List<GrammarSuggestionItem> Items { get; set; } = new();
    }

    // ============================ Lesson ============================

    /// <summary>Mục nội dung được gắn vào bài học. ExistingId null = chưa có trong hệ thống.</summary>
    public class LessonLinkedItem
    {
        /// <summary>Kanji: ký tự. Vocabulary: Word. Grammar: Title.</summary>
        public string Key { get; set; } = string.Empty;
        /// <summary>Chỉ dùng cho Vocabulary.</summary>
        public string? Reading { get; set; }
        public int? ExistingId { get; set; }
        /// <summary>True = nội dung MỚI được sinh trong cùng kế hoạch (chưa có Id), liên kết bằng khóa tự nhiên.</summary>
        public bool IsInPlan { get; set; }
    }

    public class SuggestLessonResponse
    {
        public int CertificateLevelId { get; set; }
        public int LanguageSkillId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Rationale { get; set; }
        public int SortOrder { get; set; }
        public bool IsDuplicateTitle { get; set; }
        public int? ExistingLessonId { get; set; }
        public List<LessonLinkedItem> Kanji { get; set; } = new();
        public List<LessonLinkedItem> Vocabulary { get; set; } = new();
        public List<LessonLinkedItem> Grammar { get; set; } = new();
    }

    // ============================ PracticeExercise ============================

    public class SuggestPracticeExerciseResponse
    {
        public int LessonId { get; set; }
        public int LanguageSkillId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ChoiceQuestionDraft> Questions { get; set; } = new();
    }

    // ============================ MockTest ============================

    public class MockTestSectionDraft
    {
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int CertificateSectionId { get; set; }
        public int LanguageSkillId { get; set; }
        public List<ChoiceQuestionDraft> Questions { get; set; } = new();
    }

    public class SuggestMockTestResponse
    {
        public int CertificateLevelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
        public List<MockTestSectionDraft> Sections { get; set; } = new();
    }

    // ============================ Confirm ============================

    public class SubmitKanjiApprovalResponse
    {
        public Guid BatchId { get; set; }
        public int SubmittedCount { get; set; }
    }

    public class SubmitContentApprovalResponse
    {
        public Guid BatchId { get; set; }
        public int SubmittedCount { get; set; }
    }

    // ============================ Scan ============================

    public class ContentAccuracyResult
    {
        /// <summary>Id của ContentApprovalRequest được kiểm tra.</summary>
        public int ApprovalRequestId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        /// <summary>False = loại nội dung này AI chưa hỗ trợ kiểm tra tự động.</summary>
        public bool IsSupported { get; set; }
        public bool IsAccurate { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    public class ScanContentAccuracyResponse
    {
        public List<ContentAccuracyResult> Results { get; set; } = new();
        public int IssueCount { get; set; }
    }
}