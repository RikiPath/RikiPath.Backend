namespace RikiPath.Application.Responses.PracticeTests
{
    public class TestSummaryResponse
    {
        public int PracticeTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
    }
}
