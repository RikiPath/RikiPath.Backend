namespace RikiPath.Domain.Entities;

public sealed class CourseScheduleRequest
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public StudyCourse Course { get; set; } = null!;
    public int SessionId { get; set; }
    public int RequestedById { get; set; }
    public string Kind { get; set; } = "Cancel";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Response { get; set; }
}
