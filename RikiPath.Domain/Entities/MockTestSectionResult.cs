namespace RikiPath.Domain.Entities
{
    /// <summary>Aggregated, per-section score for an attempt — powers the per-skill breakdown.</summary>
    public class MockTestSectionResult : Base
    {
        public int Id { get; set; }

        public int MockTestAttemptId { get; set; }
        public MockTestAttempt MockTestAttempt { get; set; }
        public int MockTestSectionId { get; set; }
        public MockTestSection MockTestSection { get; set; }

        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double ScorePercent { get; set; }
    }
}
