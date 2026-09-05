namespace RikiPath.Application.Responses.Notifications
{
    public class NotificationResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "srs_reminder" | "grading_result" | "consultation_reminder" | ...
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
