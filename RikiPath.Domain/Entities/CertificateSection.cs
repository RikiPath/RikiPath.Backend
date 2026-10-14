namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// Đại diện cho một phần thi/phần kiến thức trong kỳ thi chứng chỉ (VD: 言語知識, 読解, 聴解).
    /// </summary>
    public class CertificateSection : Base
    {
        public int Id { get; set; }

        /// <summary>
        /// Tên phần thi (VD: "Language Knowledge (Vocabulary/Grammar)", "Reading", "Listening").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Mã định danh phần thi (VD: "VOCAB_GRAMMAR", "READING", "LISTENING").
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Mô tả chi tiết về phần thi.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Thời lượng làm bài tính theo phút (nếu có).
        /// </summary>
        public int? DurationMinutes { get; set; }

        /// <summary>
        /// Điểm tối đa của phần thi này (VD: 60 điểm cho mỗi phần trong JLPT).
        /// </summary>
        public int MaxScore { get; set; }

        /// <summary>
        /// Điểm liệt (điểm tối thiểu cần đạt để không bị trượt).
        /// </summary>
        public int PassingScore { get; set; }

        /// <summary>
        /// Thứ tự hiển thị/sắp xếp của phần thi.
        /// </summary>
        public int SortOrder { get; set; }

        // ---- Relationships ----

        /// <summary>
        /// Thuộc cấp độ chứng chỉ nào (VD: N5, N4, N3...).
        /// </summary>
        public int CertificateLevelId { get; set; }
        public CertificateLevel CertificateLevel { get; set; } = null!;

        // ---- Navigation Property: 1 CertificateSection có nhiều MockTestSection ----
        public List<MockTestSection>? MockTestSections { get; set; }
    }
}