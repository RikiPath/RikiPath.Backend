namespace RikiPath.Application.Responses.Content
{
    public sealed class ApprovalFieldDiffResponse
    {
        /// <summary>Khóa camelCase, trùng với khóa trong DiffSummaryJson (meaning, exampleSentence, lessonTitle...).</summary>
        public string Field { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        /// <summary>Giá trị hiện tại trong DB. null khi là yêu cầu thêm mới.</summary>
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public bool IsChanged { get; set; }
    }
}
