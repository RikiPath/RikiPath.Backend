namespace RikiPath.Application.Responses.Essay
{
    public class EssayListItemResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime? ScannedAt { get; set; }
        public string PreviewText { get; set; } = string.Empty;
    }
}