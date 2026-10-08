namespace RikiPath.Application.Responses.Essay
{
    public class EssayScanResponse
    {
        public string Text { get; set; } = string.Empty;
        public DateTime? ScannedAt { get; set; }
    }
}
