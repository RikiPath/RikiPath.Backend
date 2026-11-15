namespace RikiPath.Application.Responses.MockTests
{
    public class AttemptResultResponse
    {
        public int AttemptId { get; set; }
        public int MockTestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public double TotalScore { get; set; }
        public List<int> WrongQuestionIds { get; set; } = [];
        public List<SectionResultItem> Sections { get; set; } = new();
    }

    public sealed record MockTestHistoryItem(
        int AttemptId, int MockTestId, string Title, string JlptLevelName, DateTime SubmittedAt, double TotalScore);
}
