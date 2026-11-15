namespace RikiPath.Application.Responses.Notifications
{
    public class NotificationResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public string? LinkUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool HasDeferredPushRetry { get; set; }
        public int? PushBackoffSeconds { get; set; }
    }
}
