using System;

namespace Domain.Entities
{
    /// <summary>One AI-generated learning-path suggestion for a learner.</summary>
    public class LearningPathSuggestion : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public DateTime GeneratedAt { get; set; }
        public string SuggestionJson { get; set; }
        public string? Summary { get; set; }
        public bool IsViewed { get; set; }
    }
}
