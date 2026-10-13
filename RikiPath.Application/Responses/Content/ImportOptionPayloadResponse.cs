namespace RikiPath.Application.Responses.Content
{
    public class ImportOptionPayloadResponse
    {
        public int SortOrder { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
