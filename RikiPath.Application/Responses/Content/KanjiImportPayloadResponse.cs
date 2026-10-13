namespace RikiPath.Application.Responses.Content
{
    public class KanjiImportPayloadResponse
    {
        public string Character { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? SinoVietnamese { get; set; }
        public string? OnYomi { get; set; }
        public string? KunYomi { get; set; }
        public int StrokeCount { get; set; }

        public string CertificateLevelCode { get; set; } = string.Empty;
        public int CertificateLevelId { get; set; }

        /// <summary>Optional - Kanji liên kết Lesson qua bảng LessonKanji (nhiều-nhiều), không phải FK trực tiếp.
        /// Không tham gia so sánh Modified/Unchanged ở bước diff này, chỉ mang theo cho bước Apply sau.</summary>
        public string? LessonTitle { get; set; }
    }
}

