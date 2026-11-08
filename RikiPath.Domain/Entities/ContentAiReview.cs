namespace RikiPath.Domain.Entities
{
    public class ContentAiReview : Base
    {
        public int Id { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public int ContentId { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public string IssuesJson { get; set; } = "[]";
        public int RequestedByUserId { get; set; }
        public DateTime ReviewedAt { get; set; }
    }
}