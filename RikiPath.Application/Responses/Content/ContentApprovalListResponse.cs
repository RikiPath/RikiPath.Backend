namespace RikiPath.Application.Responses.Content
{
    public sealed class ContentApprovalListResponse
    {
        public List<ContentApprovalListItemResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
