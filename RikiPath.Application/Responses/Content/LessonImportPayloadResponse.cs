namespace RikiPath.Application.Responses.Content
{
    /// <summary>Dữ liệu 1 bài học đã parse + resolve, serialize vào ContentApprovalRequest.PayloadJson. Khóa tự nhiên: Title + CertificateLevelId.</summary>
    public class LessonImportPayloadResponse
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }

        public string CertificateLevelCode { get; set; } = string.Empty;
        public int CertificateLevelId { get; set; }
        public int LanguageSkillId { get; set; }
    }
}
