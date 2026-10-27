using System;
using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>One AI-generated learning-path suggestion for a learner.</summary>
    public class RecommendedLearningPath : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public DateTime GeneratedAt { get; set; }
        public string SuggestionJson { get; set; }
        public string? Summary { get; set; }
        public bool IsViewed { get; set; }
        public int Version { get; set; }
        public LearningPathStatus Status { get; set; } = LearningPathStatus.Active;
        public int? StartingCertificateLevelId { get; set; }
        public int? TargetCertificateLevelId { get; set; }
        public int? BaseActivePathId { get; set; }
        public DateTime? PreviewExpiresAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }
}
