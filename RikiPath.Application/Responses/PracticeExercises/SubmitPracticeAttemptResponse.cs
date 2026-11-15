namespace RikiPath.Application.Responses.PracticeExercises
{
    public class SubmitPracticeAttemptResponse
    {
        public int AttemptId { get; init; }
        public double ScorePercent { get; init; }
        public int CorrectCount { get; init; }
        /// <summary>Số câu được chấm tự động (có option đúng). Câu tự luận không tính.</summary>
        public int GradedQuestions { get; init; }
        public int TotalQuestions { get; init; }
        public DateTime SubmittedAt { get; init; }
        public List<PracticeSavedAnswerResponse> Results { get; init; } = new();
    }
}
