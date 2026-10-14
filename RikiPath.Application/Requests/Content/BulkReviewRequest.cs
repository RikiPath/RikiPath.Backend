namespace RikiPath.Application.Requests.Content
{
    public class BulkReviewRequest
    {
        public List<BulkReviewItem> Items { get; set; } = new();
    }

    public class BulkReviewItem
    {
        public int RequestId { get; set; }
        public bool Approve { get; set; }
        public string? ReviewNote { get; set; }   // riêng cho request này
    }
}
