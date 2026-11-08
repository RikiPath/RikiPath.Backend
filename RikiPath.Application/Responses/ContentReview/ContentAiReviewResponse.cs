namespace RikiPath.Application.Responses.ContentReview
{
    /// <summary>Một lỗi/cảnh báo cụ thể, chỉ rõ phần nào của nội dung bị sai và vì sao.</summary>
    public class ContentReviewIssue
    {
        /// <summary>Rule (kiểm tra bằng code, chắc chắn) | AI (kiểm tra ngữ nghĩa).</summary>
        public string Source { get; set; } = "AI";
        /// <summary>Error (bắt buộc sửa) | Warning (nên xem lại) | Info</summary>
        public string Severity { get; set; } = "Warning";
        /// <summary>Lesson | Kanji | Vocabulary | Grammar — mục bị lỗi (lesson có thể chứa nhiều mục liên kết).</summary>
        public string ItemType { get; set; } = string.Empty;
        public int ItemId { get; set; }
        /// <summary>Nhãn dễ đọc cho author, ví dụ: "Từ vựng: 勉強".</summary>
        public string ItemLabel { get; set; } = string.Empty;
        /// <summary>Tên trường sai, trùng tên input trên form (Meaning, Reading, ExampleSentence...).</summary>
        public string Field { get; set; } = string.Empty;
        public string? CurrentValue { get; set; }
        /// <summary>Lý do sai — để author hiểu và tự kiểm tra.</summary>
        public string Reason { get; set; } = string.Empty;
        public string? Suggestion { get; set; }
        /// <summary>Độ tin cậy 0..1 (Rule luôn = 1).</summary>
        public double Confidence { get; set; } = 1;
    }

    public class ContentAiReviewResponse
    {
        public int ReviewId { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public int ContentId { get; set; }
        /// <summary>Passed | PassedWithWarnings | NeedsRevision</summary>
        public string Verdict { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int CheckedItemCount { get; set; }
        public DateTime ReviewedAt { get; set; }
        public List<ContentReviewIssue> Issues { get; set; } = [];
    }
}