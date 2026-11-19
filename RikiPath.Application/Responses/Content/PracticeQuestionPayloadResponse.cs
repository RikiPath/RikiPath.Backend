namespace RikiPath.Application.Responses.Content
{
    public class PracticeQuestionPayloadResponse
    {
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? Explanation { get; set; }
        public List<ImportOptionPayloadResponse> Options { get; set; } = [];
    }
}
