namespace RikiPath.Application.Responses.Content
{
    public class MockQuestionPayloadResponse
    {
        /// <summary>Số thứ tự câu trong CẢ đề (không reset theo từng phần).</summary>
        public int SortOrder { get; set; }

        public string QuestionText { get; set; } = string.Empty;
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Explanation { get; set; }
        public List<ImportOptionPayloadResponse> Options { get; set; } = [];
    }
}
