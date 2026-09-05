namespace RikiPath.Application.Responses.Content
{
    public class RowError
    {
        public int RowNumber { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ImportResultResponse
    {
        public int BatchId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<RowError> Errors { get; set; } = new();
    }

    public class ContentBatchResponse
    {
        public int BatchId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectReason { get; set; }
    }
}
