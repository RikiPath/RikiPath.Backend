using System;

namespace RikiPath.Domain.Entities
{
    /// <summary>The AI grading pipeline's output for one submission — item-level feedback.</summary>
    public class GradingResult : Base
    {
        public int Id { get; set; }

        public int PracticeSubmissionId { get; set; }
        public PracticeSubmission PracticeSubmission { get; set; }

        public double OverallScore { get; set; }
        public string FeedbackJson { get; set; }
        public DateTime GradedAt { get; set; }
    }
}
