namespace RikiPath.Application.Responses.Content
{
    public class BulkReviewResponse
    {
        public int SuccessCount { get; set; }

        /// <summary>Các request không xử lý được (đã bị xử lý trước đó, không tồn tại, hoặc lỗi khi map dữ liệu) kèm lý do.</summary>
        public List<BulkReviewItemResult> Failures { get; set; } = new();
    }

    public class BulkReviewItemResult
    {
        public int RequestId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
