namespace RikiPath.Application.Responses.Essay;

public class EssayScanResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string OriginalOcrText { get; set; } = string.Empty;
    public string ContentText { get; set; } = string.Empty;
    public DateTime? ScannedAt { get; set; }
}

public class EssayListItemResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime? ScannedAt { get; set; }
    public string PreviewText { get; set; } = string.Empty;
}

public class EssayDetailResponse : EssayScanResponse
{
}
