namespace RikiPath.Domain.Entities
{
    public class PracticeQuestionOption : Base
    {
        public int Id { get; set; }
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
        public int SortOrder { get; set; }

        public int PracticeQuestionId { get; set; }
        public PracticeQuestion PracticeQuestion { get; set; }
    }
}
