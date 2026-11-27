namespace RikiPath.Application.Responses.MockTests
{
    public class DetailedAttemptResultResponse
    {
        public int AttemptId { get; set; }
        public int MockTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public string ExamKind { get; set; } = "FullMockTest";
        public string AttemptStatus { get; set; } = "completed";
        public double TotalScore { get; set; }
        public double AccuracyPercent { get; set; }
        public MockAttemptScoringSummary? Scoring { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int BlankCount { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int DurationSeconds { get; set; }
        public List<QuestionReviewCard> Questions { get; set; } = [];
    }
}
