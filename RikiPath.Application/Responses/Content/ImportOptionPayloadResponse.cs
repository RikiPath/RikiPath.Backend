namespace RikiPath.Application.Responses.Content
{
    public class ImportOptionPayloadResponse
    {
        public int SortOrder { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        /// <summary>Giải thích riêng cho lựa chọn này (đúng hoặc sai), lấy từ Excel.</summary>
        public string? Explanation { get; set; }
        public bool IsCorrect { get; set; }
    }
}
