namespace RikiPath.Application.Responses.MockTests
{
    public class SectionResultItem
    {
        public int MockTestSectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string LanguageSkillName { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double Score { get; set; }
    }
}
