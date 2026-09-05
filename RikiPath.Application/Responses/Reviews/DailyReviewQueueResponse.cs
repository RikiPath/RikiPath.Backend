namespace RikiPath.Application.Responses.Reviews
{
    public class DailyReviewQueueResponse
    {
        public int TotalDue { get; set; }
        public List<ReviewQueueItemResponse> Items { get; set; } = new();
    }
}
