namespace RikiPath.Application.Responses.MockTests
{
    public class TestSummaryResponse
    {
        public int MockTestId { get; set; }
        public int CertificateLevelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JlptLevelName { get; set; } = string.Empty;
        public string ExamKind { get; set; } = "FullMockTest";
        public int Version { get; set; } = 1;
        public int TimeLimitMinutes { get; set; }
        public int QuestionCount { get; set; }
        public List<string> IncludedSkills { get; set; } = [];
        public List<TestSectionSummaryResponse> Sections { get; set; } = [];
    }

    public class TestSectionSummaryResponse
    {
        public string Title { get; set; } = string.Empty;
        public string SkillName { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int? DurationMinutes { get; set; }
        public bool RequiresAudio { get; set; }
    }
}
