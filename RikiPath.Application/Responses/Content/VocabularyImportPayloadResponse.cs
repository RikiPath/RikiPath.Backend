namespace RikiPath.Application.Responses.Content
{
    /// <summary>Dữ liệu 1 từ vựng đã parse + resolve, serialize vào ContentApprovalRequest.PayloadJson. Khóa tự nhiên: Word + Reading.</summary>
    public class VocabularyImportPayloadResponse
    {
        public string Word { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ExampleSentenceMeaning { get; set; }

        /// <summary>Optional. Null = không đổi giá trị đang có.</summary>
        public string? AudioUrl { get; set; }

        public string CertificateLevelCode { get; set; } = string.Empty;
        public int CertificateLevelId { get; set; }
        public string? LessonTitle { get; set; }
    }
}
