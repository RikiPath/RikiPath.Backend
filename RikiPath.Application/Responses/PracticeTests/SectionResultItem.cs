namespace RikiPath.Application.Responses.PracticeTests
{
    public class SectionResultItem
    {
        public int PracticeTestSectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string SkillName { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double Score { get; set; }
    }
}
