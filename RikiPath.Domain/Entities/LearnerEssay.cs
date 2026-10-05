namespace RikiPath.Domain.Entities;

public class LearnerEssay : Base
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageStoragePath { get; set; } = string.Empty;
    public string OriginalOcrText { get; set; } = string.Empty;
    public string ContentText { get; set; } = string.Empty;
    public string OcrLanguage { get; set; } = "jpn";
}
