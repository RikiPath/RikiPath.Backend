namespace RikiPath.Application.Responses.PracticeTests
{
    public class AttemptResultResponse
    {
        public int AttemptId { get; set; }
        public int PracticeTestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public double TotalScore { get; set; }
        public List<SectionResultItem> Sections { get; set; } = new();
    }
}
