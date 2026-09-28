namespace RikiPath.Domain.Entities
{
    /// <summary>The option a learner picked for a single question within an attempt.</summary>
    public class MockTestAnswer : Base
    {
        public int Id { get; set; }

        public int MockTestAttemptId { get; set; }
        public MockTestAttempt MockTestAttempt { get; set; }
        public int MockQuestionId { get; set; }
        public MockQuestion MockQuestion { get; set; }
        public int? SelectedOptionId { get; set; }
        public MockQuestionOption? SelectedOption { get; set; }

        public bool IsCorrect { get; set; }
    }
}
