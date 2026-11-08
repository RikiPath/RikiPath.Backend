namespace RikiPath.Application.Requests.ContentAuthoring
{
    // ============================ Dùng chung ============================

    public class ChoiceOptionDraft
    {
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public class ChoiceQuestionDraft
    {
        public string QuestionText { get; set; } = string.Empty;
        /// <summary>Chỉ dùng cho phần Nghe.</summary>
        public string? AudioScript { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public List<ChoiceOptionDraft> Options { get; set; } = new();
    }

    // ============================ Kanji ============================

    public class SuggestKanjiRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public int MaxItems { get; set; } = 10;
        public int? LessonId { get; set; }
        public int? CertificateLevelId { get; set; }
    }

    public class ConfirmKanjiItem
    {
        public string Character { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? SinoVietnamese { get; set; }
        public string? OnYomi { get; set; }
        public string? KunYomi { get; set; }
        public int StrokeCount { get; set; }
        public bool IsDuplicate { get; set; }
        public int? ExistingKanjiId { get; set; }
    }

    public class ConfirmKanjiSuggestionsRequest
    {
        public int? LessonId { get; set; }
        public int? CertificateLevelId { get; set; }
        public List<ConfirmKanjiItem> Items { get; set; } = new();
    }

    // ============================ Vocabulary ============================

    public class SuggestVocabularyRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public int MaxItems { get; set; } = 10;
        public int? LessonId { get; set; }
        public int? CertificateLevelId { get; set; }
    }

    public class ConfirmVocabularyItem
    {
        public string Word { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }
        public bool IsDuplicate { get; set; }
        public int? ExistingVocabularyId { get; set; }
    }

    public class ConfirmVocabularySuggestionsRequest
    {
        public int? LessonId { get; set; }
        public int? CertificateLevelId { get; set; }
        public List<ConfirmVocabularyItem> Items { get; set; } = new();
    }

    // ============================ Grammar ============================

    public class SuggestGrammarRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public int MaxItems { get; set; } = 10;
        public int? LessonId { get; set; }
        public int? CertificateLevelId { get; set; }
    }

    public class ConfirmGrammarItem
    {
        public string Title { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
        public string? UsageNotes { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }
        public bool IsDuplicate { get; set; }
        public int? ExistingGrammarPatternId { get; set; }
    }

    public class ConfirmGrammarSuggestionsRequest
    {
        public int? LessonId { get; set; }
        public int? CertificateLevelId { get; set; }
        public List<ConfirmGrammarItem> Items { get; set; } = new();
    }

    // ============================ Lesson ============================

    public class SuggestLessonRequest
    {
        public string Prompt { get; set; } = string.Empty;
        /// <summary>Tùy chọn: nếu gửi thì cấp độ phải thuộc loại chứng chỉ này.</summary>
        public int? CertificateTypeId { get; set; }
        public int CertificateLevelId { get; set; }
        public int LanguageSkillId { get; set; }
        public int MaxItemsPerType { get; set; } = 8;
    }

    public class ConfirmLessonSuggestionRequest
    {
        public int? CertificateTypeId { get; set; }
        public int CertificateLevelId { get; set; }
        public int LanguageSkillId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public List<int> KanjiIds { get; set; } = new();
        public List<int> VocabularyIds { get; set; } = new();
        public List<int> GrammarPatternIds { get; set; } = new();
    }

    // ============================ PracticeExercise ============================

    public class SuggestPracticeExerciseRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public int LessonId { get; set; }
        /// <summary>Null = lấy theo kỹ năng của bài học.</summary>
        public int? LanguageSkillId { get; set; }
        public int QuestionCount { get; set; } = 5;
    }

    public class ConfirmPracticeExerciseRequest
    {
        public int LessonId { get; set; }
        public int LanguageSkillId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ChoiceQuestionDraft> Questions { get; set; } = new();
    }

    // ============================ MockTest ============================

    public class MockTestSectionPlan
    {
        public int CertificateSectionId { get; set; }
        public int LanguageSkillId { get; set; }
        public int QuestionCount { get; set; } = 5;
        /// <summary>Null/trống = lấy tên của CertificateSection.</summary>
        public string? Title { get; set; }
    }

    public class SuggestMockTestRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public int? CertificateTypeId { get; set; }
        public int CertificateLevelId { get; set; }
        public string? Title { get; set; }
        public int? TimeLimitMinutes { get; set; }
        public List<MockTestSectionPlan> Sections { get; set; } = new();
    }

    public class ConfirmMockTestSection
    {
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int CertificateSectionId { get; set; }
        public int LanguageSkillId { get; set; }
        public List<ChoiceQuestionDraft> Questions { get; set; } = new();
    }

    public class ConfirmMockTestRequest
    {
        public int? CertificateTypeId { get; set; }
        public int CertificateLevelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
        public List<ConfirmMockTestSection> Sections { get; set; } = new();
    }

    // ============================ Scan ============================

    public class ScanContentAccuracyRequest
    {
        /// <summary>Chỉ quét một lần gửi cụ thể. Null = quét toàn bộ Draft hiện có của author.</summary>
        public Guid? BatchId { get; set; }
    }
}