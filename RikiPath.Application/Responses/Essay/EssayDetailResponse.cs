namespace RikiPath.Application.Responses.Essay
{
    public class EssayDetailResponse
    {
        public string OriginalOcrText { get; set; } = string.Empty;
        public string ContentText { get; set; } = string.Empty;
        public DateTime? ScannedAt { get; set; }
    }
}
