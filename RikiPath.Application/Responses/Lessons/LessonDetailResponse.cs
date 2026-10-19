namespace RikiPath.Application.Responses.Lessons
{
    public class LessonDetailResponse
    {
        public int LessonId { get; set; }
        public int CourseId { get; set; }
        public int CertificateLevelId { get; set; }
        public string LevelCode { get; set; } = string.Empty;
        public int LanguageSkillId { get; set; }
        public string LanguageSkillName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public string? AttachmentUrl { get; set; }
        public int OrderIndex { get; set; }

        public int ProgressPercent { get; set; }
        public int ResumePositionSeconds { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
