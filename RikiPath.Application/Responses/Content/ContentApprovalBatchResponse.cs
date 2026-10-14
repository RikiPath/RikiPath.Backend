namespace RikiPath.Application.Responses.Content
{
    public sealed class ContentApprovalBatchResponse
    {
        public Guid BatchId { get; set; }
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ModifiedCount { get; set; }
        public List<ContentApprovalDetailResponse> Items { get; set; } = new();
    }
}
