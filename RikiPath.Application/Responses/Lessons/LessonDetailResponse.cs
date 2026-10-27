namespace RikiPath.Application.Responses.Lessons
{
    public class LessonDetailResponse
    {
        public int LessonId { get; set; }
        public int CourseId { get; set; }
        public int CertificateLevelId { get; set; }
        public string LevelCode { get; set; } = string.Empty;
        public int LanguageSkillId { get; set; }
        public string LanguageSkillName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public string? AttachmentUrl { get; set; }
        public int OrderIndex { get; set; }

        public int ProgressPercent { get; set; }
        public int ResumePositionSeconds { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }

        public List<LessonVocabularyResponse> Vocabularies { get; set; } = new();
        public List<LessonKanjiResponse> Kanjis { get; set; } = new();
        public List<LessonGrammarResponse> Grammars { get; set; } = new();
    }

    public class LessonVocabularyResponse
    {
        public int VocabularyId { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }
        public string? AudioUrl { get; set; }
    }

    public class LessonKanjiResponse
    {
        public int KanjiId { get; set; }
        public string Character { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? SinoVietnamese { get; set; }
        public string? OnYomi { get; set; }
        public string? KunYomi { get; set; }
        public int StrokeCount { get; set; }
        public string? StrokeOrderImageUrl { get; set; }
        public string? AudioUrl { get; set; }
    }

    public class LessonGrammarResponse
    {
        public int GrammarPatternId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
        public string? UsageNotes { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }
    }
}
