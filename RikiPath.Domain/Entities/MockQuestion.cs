namespace RikiPath.Domain.Entities;

/// <summary>A question in a mock test section.</summary>
public class MockQuestion : Base
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }
    public int SortOrder { get; set; }
    public int MockTestSectionId { get; set; }
    public MockTestSection MockTestSection { get; set; } = null!;
    public List<MockQuestionOption>? Options { get; set; }
    public List<MockTestAnswer>? Answers { get; set; }
}
