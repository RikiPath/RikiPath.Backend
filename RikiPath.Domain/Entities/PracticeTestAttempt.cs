using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>One learner's attempt at a mock JLPT practice test.</summary>
    public class PracticeTestAttempt : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }
        public int PracticeTestId { get; set; }
        public PracticeTest PracticeTest { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsCompleted { get; set; }
        public double? TotalScore { get; set; }

        public List<PracticeTestAnswer>? Answers { get; set; }
        public List<PracticeTestSectionResult>? SectionResults { get; set; }
    }
}
