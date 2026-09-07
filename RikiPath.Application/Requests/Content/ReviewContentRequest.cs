namespace RikiPath.Application.Requests.Content
{
    public class ReviewContentRequest
    {
        public bool Approve { get; set; }
        public string? ReviewNote { get; set; }
    }
}
