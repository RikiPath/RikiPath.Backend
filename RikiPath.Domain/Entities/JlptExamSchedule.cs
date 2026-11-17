namespace RikiPath.Domain.Entities;

public sealed class JlptExamSchedule
{
    public int Id { get; set; }
    public string SourceKey { get; set; } = "";
    public DateOnly ExamDate { get; set; }
    public string SourceUrl { get; set; } = "";
    public string Location { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}
