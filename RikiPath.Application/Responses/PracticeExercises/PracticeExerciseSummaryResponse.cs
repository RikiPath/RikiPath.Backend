namespace RikiPath.Application.Responses.PracticeExercises
{
    public class PracticeLatestAttemptResponse
    {
        public int AttemptId { get; init; }
        public bool IsCompleted { get; init; }
        public double? ScorePercent { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime? SubmittedAt { get; init; }
    }
}
