namespace RikiPath.Application.Responses.Content
{
    public class MockTestSectionPayloadResponse
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>Thứ tự xuất hiện của phần trong file (1..n).</summary>
        public int SortOrder { get; set; }

        public int LanguageSkillId { get; set; }
        public string LanguageSkillName { get; set; } = string.Empty;
        public List<MockQuestionPayloadResponse> Questions { get; set; } = [];
    }
}
