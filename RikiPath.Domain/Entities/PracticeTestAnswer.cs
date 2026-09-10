namespace RikiPath.Domain.Entities
{
    /// <summary>The option a learner picked for a single question within an attempt.</summary>
    public class PracticeTestAnswer : Base
    {
        public int Id { get; set; }

        public int PracticeTestAttemptId { get; set; }
        public PracticeTestAttempt PracticeTestAttempt { get; set; }
        public int PracticeQuestionId { get; set; }
        public PracticeQuestion PracticeQuestion { get; set; }
        public int? SelectedOptionId { get; set; }
        public PracticeQuestionOption? SelectedOption { get; set; }

        public bool IsCorrect { get; set; }
    }
}
