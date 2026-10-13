namespace RikiPath.Application.Responses.Content
{
    /// <summary>Dữ liệu 1 mẫu ngữ pháp đã parse + resolve, serialize vào ContentApprovalRequest.PayloadJson. Khóa tự nhiên: Title.</summary>
    public class GrammarImportPayloadResponse
    {
        public string Title { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
        public string? UsageNotes { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }

        public string CertificateLevelCode { get; set; } = string.Empty;
        public int CertificateLevelId { get; set; }
        public string? LessonTitle { get; set; }
    }
}
