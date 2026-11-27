namespace RikiPath.Application.Responses.Content
{
    public class MockQuestionPayloadResponse
    {
        /// <summary>Số thứ tự câu trong CẢ đề (không reset theo từng phần).</summary>
        public int SortOrder { get; set; }

        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "SingleChoice";
        /// <summary>Type-specific settings, such as draggable tokens or blank positions.</summary>
        public System.Text.Json.JsonElement? InteractionData { get; set; }
        /// <summary>Structured correct answer for non-option interactions, for example token order.</summary>
        public System.Text.Json.JsonElement? AnswerKey { get; set; }
        public string? AudioUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Explanation { get; set; }
        public List<ImportOptionPayloadResponse> Options { get; set; } = [];
    }
}
