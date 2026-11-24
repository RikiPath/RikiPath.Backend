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
        /// <summary>Structured learner response, such as ordered token IDs or blank-to-answer mappings.</summary>
        public string? ResponseJson { get; set; }

        public bool IsCorrect { get; set; }

        /// <summary>Persisted AI explanation for this learner's answer, including every option's reason.</summary>
        public string? AiExplanationJson { get; set; }

        /// <summary>Input snapshot used to generate the persisted explanation, retained for future training/export.</summary>
        public string? AiExplanationInputJson { get; set; }

        public DateTime? AiExplanationGeneratedAtUtc { get; set; }
        public int AiExplanationVersion { get; set; }
    }
}
