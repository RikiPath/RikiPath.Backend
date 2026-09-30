using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>One learner's attempt at a mock JLPT practice test.</summary>
    public class MockTestAttempt : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }
        public int MockTestId { get; set; }
        public MockTest MockTest { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsCompleted { get; set; }
        public double? TotalScore { get; set; }

        public List<MockTestAnswer>? Answers { get; set; }
        public List<MockTestSectionResult>? SectionResults { get; set; }
    }
}
