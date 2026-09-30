namespace RikiPath.Domain.Entities;

/// <summary>An answer choice for a mock test question.</summary>
public class MockQuestionOption : Base
{
    public int Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
    public int MockQuestionId { get; set; }
    public MockQuestion MockQuestion { get; set; } = null!;
}
