namespace Domain.Entities
{
    /// <summary>In-app notification (consultation answered, content approved/rejected, grading finished, etc.).</summary>
    public class Notification : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }

        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public string? LinkUrl { get; set; }
    }
}
