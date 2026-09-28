namespace RikiPath.Domain.Entities;

/// <summary>An answer choice for a practice question.</summary>
public class PracticeQuestionOption : Base
{
    public int Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
    public int PracticeQuestionId { get; set; }
    public PracticeQuestion PracticeQuestion { get; set; } = null!;
}
