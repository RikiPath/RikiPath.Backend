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
        public List<PracticeAnswerResultResponse> Results { get; init; } = new();
    }

    public class PracticeAnswerResultResponse
    {
        public int QuestionId { get; init; }
        public int? SelectedOptionId { get; init; }
        public string? TextAnswer { get; init; }
        public int? CorrectOptionId { get; init; }
        /// <summary>null với câu không chấm tự động.</summary>
        public bool? IsCorrect { get; init; }
        public string? Explanation { get; init; }
    }
}
