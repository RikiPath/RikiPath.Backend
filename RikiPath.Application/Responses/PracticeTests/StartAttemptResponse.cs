namespace RikiPath.Application.Responses.PracticeTests
{
    public class StartAttemptResponse
    {
        public int AttemptId { get; set; }
        public int PracticeTestId { get; set; }
        public DateTime StartedAt { get; set; }
        public int TimeLimitMinutes { get; set; }
        public int TotalPreviouslyWrongQuestions { get; set; }
        public List<int> PreviouslyWrongQuestionIds { get; set; } = [];
    }
}
