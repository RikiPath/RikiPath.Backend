namespace RikiPath.Application.Responses.Essay
{
    public class EssayDetailResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string OriginalOcrText { get; set; } = string.Empty;
        public string ContentText { get; set; } = string.Empty;
        public DateTime? ScannedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }
}
