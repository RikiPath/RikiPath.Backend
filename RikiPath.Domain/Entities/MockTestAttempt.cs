using System;
using System.Collections.Generic;
using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>One learner's attempt at a mock JLPT practice test or placement test.</summary>
    public class MockTestAttempt : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public AttemptKind AttemptKind { get; set; } = AttemptKind.MockTest;
        public DateTime StartedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsCompleted { get; set; }
        public double? TotalScore { get; set; }
        /// <summary>Marks the learner's onboarding placement assessment attempt.</summary>
        public bool IsPlacementAssessment { get; set; }

        public List<MockTestAnswer>? Answers { get; set; }
        public List<MockTestSectionResult>? SectionResults { get; set; }
    }
}
