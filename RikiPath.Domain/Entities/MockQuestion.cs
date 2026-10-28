namespace RikiPath.Domain.Entities;

/// <summary>A question in a mock test section.</summary>
public class MockQuestion : Base
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    /// <summary>Question interaction kind, e.g. SingleChoice, MultipleChoice, Ordering, FillBlank, or ShortAnswer.</summary>
    public string QuestionType { get; set; } = "SingleChoice";
    /// <summary>Interaction metadata shown to the client (tokens, blanks, matching columns, etc.).</summary>
    public string? InteractionDataJson { get; set; }
    /// <summary>Structured answer definition for interactions that cannot be represented by one option ID.</summary>
    public string? AnswerKeyJson { get; set; }
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }
    public int SortOrder { get; set; }
    public int MockTestSectionId { get; set; }
    public MockTestSection MockTestSection { get; set; } = null!;
    public List<MockQuestionOption>? Options { get; set; }
    public List<MockTestAnswer>? Answers { get; set; }
}
