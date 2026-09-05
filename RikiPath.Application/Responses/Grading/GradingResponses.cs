namespace RikiPath.Application.Responses.Grading
{
    public class SubmitEssayResponse
    {
        public int SubmissionId { get; set; }
        public string Status { get; set; } = string.Empty; // "graded" | "grading_failed"
    }

    public class GradingIssueItem
    {
        public string Category { get; set; } = string.Empty;   // "grammar" | "vocabulary" | "kanji_accuracy" | "structure"
        public string OriginalText { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }

    public class GradingResultResponse
    {
        public int SubmissionId { get; set; }
        public double Score { get; set; } // 0-100
        public string OverallFeedback { get; set; } = string.Empty;
        public List<GradingIssueItem> Issues { get; set; } = new();
        public DateTime GradedAt { get; set; }
    }
}
