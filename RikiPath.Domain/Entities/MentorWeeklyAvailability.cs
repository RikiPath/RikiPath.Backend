namespace RikiPath.Domain.Entities;

/// <summary>A whole morning, afternoon or evening offered by a mentor, never a meeting room.</summary>
public sealed class MentorWeeklyAvailability : Base
{
    public int Id { get; set; }
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Period { get; set; } = string.Empty;
    public uint Version { get; set; }
    public ICollection<MentorAvailability> Slots { get; set; } = new List<MentorAvailability>();
}
