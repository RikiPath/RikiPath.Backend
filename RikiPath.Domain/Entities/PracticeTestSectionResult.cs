namespace RikiPath.Domain.Entities
{
    /// <summary>Aggregated, per-section score for an attempt — powers the per-skill breakdown.</summary>
    public class PracticeTestSectionResult : Base
    {
        public int Id { get; set; }

        public int PracticeTestAttemptId { get; set; }
        public PracticeTestAttempt PracticeTestAttempt { get; set; }
        public int PracticeTestSectionId { get; set; }
        public PracticeTestSection PracticeTestSection { get; set; }

        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double ScorePercent { get; set; }
    }
}
