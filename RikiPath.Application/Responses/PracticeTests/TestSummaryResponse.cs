namespace RikiPath.Application.Responses.MockTests
{
    public class TestSummaryResponse
    {
        public int MockTestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
    }
}
