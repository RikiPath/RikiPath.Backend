namespace RikiPath.Application.Responses.Content
{
    public class PracticeExerciseImportPayloadResponse
    {
        public string? ImageUrl { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public string Scope { get; set; } = "LessonQuickCheck";
        public double PassingScorePercent { get; set; } = 70;

        public string LessonTitle { get; set; } = string.Empty;

        /// <summary>Null khi Lesson mới chỉ có bản nháp chờ duyệt - lúc Admin duyệt phải tra lại Lesson theo LessonTitle + CertificateLevelId.</summary>
        public int? LessonId { get; set; }

        public int? TopicId { get; set; }
        public string? TopicTitle { get; set; }

        /// <summary>CertificateLevelId của Lesson / Stage.</summary>
        public int CertificateLevelId { get; set; }
        public string? CertificateLevelCode { get; set; }

        public int LanguageSkillId { get; set; }
        public string LanguageSkillName { get; set; } = string.Empty;
        public List<PracticeQuestionPayloadResponse> Questions { get; set; } = [];
    }

}
