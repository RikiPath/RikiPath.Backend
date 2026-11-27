namespace RikiPath.Application.Responses.MockTests
{
    public class StartAttemptResponse
    {
        public int AttemptId { get; set; }
        public int MockTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public string ExamKind { get; set; } = "FullMockTest";
        public bool EnforceStrictTimer { get; set; } = true;
        public bool IsResumed { get; set; }
        public string AttemptStatus { get; set; } = "in_progress";
        public DateTime StartedAt { get; set; }
        public DateTime ServerNow { get; set; }
        public DateTime? LastSavedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int TimeLimitMinutes { get; set; }
        public long LatestAnswerSequence { get; set; }
        public int? ElapsedSeconds { get; set; }
        public int TotalPreviouslyWrongQuestions { get; set; }
        public List<int> PreviouslyWrongQuestionIds { get; set; } = [];
        public List<StartAttemptQuestionResponse> Questions { get; set; } = [];
    }

    public class StartAttemptQuestionResponse
    {
        public int QuestionId { get; set; }
        public int MockTestSectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string? LanguageSkillName { get; set; }
        public string? QuestionGroupTitle { get; set; }
        public int SectionOrder { get; set; }
        public int Order { get; set; }
        public string Content { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "SingleChoice";
        public System.Text.Json.JsonElement? InteractionData { get; set; }
        public int? SelectedOptionId { get; set; }
        public System.Text.Json.JsonElement? Response { get; set; }
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public int? PassageId { get; set; }
        public string? PassageTitle { get; set; }
        public string? PassageText { get; set; }
        public string? PassageInstructions { get; set; }
        public string? PassageImageUrl { get; set; }
        public string? TemplateKind { get; set; }
        public DTOs.ContentAuthoring.ListeningPlaybackPolicyDto? PlaybackPolicy { get; set; }
        public List<StartAttemptOptionResponse> Options { get; set; } = [];
    }

    public class StartAttemptOptionResponse
    {
        public int OptionId { get; set; }
        public string? ImageUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
