namespace RikiPath.Application.Responses.MockTests
{
    public class TestSummaryResponse
    {
        public int MockTestId { get; set; }
        public int CertificateLevelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public string ExamKind { get; set; } = "FullMockTest";
        public int Version { get; set; } = 1;
        public int TimeLimitMinutes { get; set; }
        public int QuestionCount { get; set; }
        public List<string> IncludedSkills { get; set; } = [];
        public List<TestSectionSummaryResponse> Sections { get; set; } = [];

        // Per-learner lifecycle & entitlement state
        public string UserStatus { get; set; } = "NOT_STARTED";
        public int? ActiveAttemptId { get; set; }
        public int? ActiveAnsweredCount { get; set; }
        public int? ActiveTotalQuestions { get; set; }
        public int? ActiveProgressPercent { get; set; }
        public DateTime? ActiveLastSavedAt { get; set; }
        public DateTime? ActiveExpiresAt { get; set; }
        public int? ActiveRemainingSeconds { get; set; }
        public int? ActiveElapsedSeconds { get; set; }
        public int? LatestCompletedAttemptId { get; set; }
        public double? LatestScore { get; set; }
        public MockAttemptScoringSummary? LatestScoring { get; set; }
        public DateTime? LatestSubmittedAt { get; set; }
        public int? LatestCorrectCount { get; set; }
        public int? LatestTotalQuestions { get; set; }
        public int AttemptsCompletedCount { get; set; }
        public bool CanStart { get; set; } = true;
        public bool CanResume { get; set; }
        public bool CanRestart { get; set; }
        public bool CanDownloadPdf { get; set; }
        public string? UnavailableReason { get; set; }
        public bool EnforceStrictTimer { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class TestSectionSummaryResponse
    {
        public int SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SkillName { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int? DurationMinutes { get; set; }
        public bool RequiresAudio { get; set; }
    }

    public sealed class PracticeSetPreviewResponse
    {
        public int MockTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public string ExamKind { get; set; } = "PracticeSet";
        public int Version { get; set; } = 1;
        public int TimeLimitMinutes { get; set; }
        public int QuestionCount { get; set; }
        public List<string> IncludedSkills { get; set; } = [];
        public string UserStatus { get; set; } = "NOT_STARTED";
        public int? ActiveAttemptId { get; set; }
        public int? ActiveAnsweredCount { get; set; }
        public int? ActiveTotalQuestions { get; set; }
        public int? ActiveProgressPercent { get; set; }
        public DateTime? ActiveLastSavedAt { get; set; }
        public bool CanStart { get; set; } = true;
        public bool CanDownloadPdf { get; set; } = true;
        public string? UnavailableReason { get; set; }
        public List<PracticeSetPreviewSectionResponse> Sections { get; set; } = [];
    }

    public sealed class PracticeSetPreviewSectionResponse
    {
        public int SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SkillName { get; set; } = string.Empty;
        public int? DurationMinutes { get; set; }
        public int QuestionCount { get; set; }
        public bool RequiresAudio { get; set; }
        public List<PracticeSetPreviewQuestionResponse> Questions { get; set; } = [];
    }

    public sealed class PracticeSetPreviewQuestionResponse
    {
        public int QuestionId { get; set; }
        public int Order { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "SingleChoice";
        public string SkillName { get; set; } = string.Empty;
        public string? QuestionGroupTitle { get; set; }
        public int? PassageId { get; set; }
        public string? PassageTitle { get; set; }
        public string? PassageText { get; set; }
        public string? PassageInstructions { get; set; }
        public bool HasAudio { get; set; }
        public bool HasImage { get; set; }
        public bool IsConfidentialRedacted { get; set; }
        public List<PracticeSetPreviewOptionResponse> Options { get; set; } = [];
    }

    public sealed class PracticeSetPreviewOptionResponse
    {
        public int Order { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool HasImage { get; set; }
    }
}
