namespace RikiPath.Application.Responses.MockTests
{
    public class SectionResultItem
    {
        private double _score;

        public int MockTestSectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string LanguageSkillName { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }

        public double Score
        {
            get => _score;
            set => _score = value;
        }

        public double ScorePercent
        {
            get => _score;
            set => _score = value;
        }
    }
}
