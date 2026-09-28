using RikiPath.Application.DTOs.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Content;

public class AuthorContentDetailsResponse
{
    public int Id { get; set; }
    public ContentEntityType EntityType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? VideoUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public int? SortOrder { get; set; }
    public int CertificateLevelId { get; set; }
    public int? LanguageSkillId { get; set; }
    public string? Character { get; set; }
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
    public int ContentAuthorId { get; set; }
    public ContentStatus Status { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewedByName { get; set; }
    public List<AuthorMockSectionDetails>? MockSections { get; set; }
    public List<AuthorPracticeQuestionDetails>? PracticeQuestions { get; set; }
}

public class AuthorMockSectionDetails
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int LanguageSkillId { get; set; }
    public List<AuthorQuestionDetails> Questions { get; set; } = [];
}

public class AuthorQuestionDetails
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public List<AuthorOptionDetails> Options { get; set; } = [];
}

public class AuthorPracticeQuestionDetails
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public int SortOrder { get; set; }
    public List<AuthorOptionDetails> Options { get; set; } = [];
}

public class AuthorOptionDetails
{
    public int Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}
