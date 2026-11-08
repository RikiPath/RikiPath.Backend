namespace RikiPath.Domain.Entities;

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
