using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// Kết quả một lần AI kiểm tra nội dung của Content Author.
    /// Không FK cứng tới Lesson/Kanji/... (polymorphic: ContentType + ContentId).
    /// </summary>
    public class ContentAiReview : Base
    {
        public int Id { get; set; }
        public AiReviewContentType ContentType { get; set; }
        public int ContentId { get; set; }

        /// <summary>Passed | PassedWithWarnings | NeedsRevision</summary>
        public string Verdict { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        /// <summary>JSON danh sách ContentReviewIssue.</summary>
        public string IssuesJson { get; set; } = "[]";

        public int RequestedByUserId { get; set; }
        public DateTime ReviewedAt { get; set; }
    }
}