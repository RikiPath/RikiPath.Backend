namespace RikiPath.Application.Responses.MockTests
{
    public class StartAttemptResponse
    {
        public int AttemptId { get; set; }
        public int MockTestId { get; set; }
        public DateTime StartedAt { get; set; }
        public int TimeLimitMinutes { get; set; }
        public int TotalPreviouslyWrongQuestions { get; set; }
        public List<int> PreviouslyWrongQuestionIds { get; set; } = [];
        public List<StartAttemptQuestionResponse> Questions { get; set; } = [];
    }

    public class StartAttemptQuestionResponse
    {
        public int QuestionId { get; set; }
        public int MockTestSectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public int SectionOrder { get; set; }
        public int Order { get; set; }
        public string Content { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "SingleChoice";
        public System.Text.Json.JsonElement? InteractionData { get; set; }
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public List<StartAttemptOptionResponse> Options { get; set; } = [];
    }

    public class StartAttemptOptionResponse
    {
        public int OptionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
