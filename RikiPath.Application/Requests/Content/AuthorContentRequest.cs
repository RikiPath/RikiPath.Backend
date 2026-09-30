namespace RikiPath.Application.Requests.Content;

/// <summary>Input for the matching content type; fields unrelated to that type are ignored.</summary>
public class AuthorContentRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? VideoUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public int? SortOrder { get; set; }
    public int CertificateLevelId { get; set; }
    public int? LanguageSkillId { get; set; }

    public string? Character { get; set; }
    public RikiPath.Domain.Enums.KanaCharacterType? KanaType { get; set; }
    public string? Romaji { get; set; }
    public string? Meaning { get; set; }
    public string? SinoVietnamese { get; set; }
    public string? OnYomi { get; set; }
    public string? KunYomi { get; set; }
    public int? StrokeCount { get; set; }
    public string? StrokeOrderImageUrl { get; set; }
    public string? AudioUrl { get; set; }

    public string? Word { get; set; }
    public string? Reading { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleSentenceMeaning { get; set; }

    public string? Structure { get; set; }
    public string? UsageNotes { get; set; }

    public int? TimeLimitMinutes { get; set; }
    public int? LessonId { get; set; }
}
