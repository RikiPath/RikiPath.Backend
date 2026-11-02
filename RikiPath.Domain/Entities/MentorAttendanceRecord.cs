using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities;

public sealed class MentorAttendanceRecord
{
    public int Id { get; set; }
    public int MentorBookingId { get; set; }
    public MentorBooking MentorBooking { get; set; } = null!;
    public Guid RoomId { get; set; }
    public int UserId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public MentorAttendanceStatus Status { get; set; } = MentorAttendanceStatus.NotJoined;
    public DateTime? FirstJoinedAtUtc { get; set; }
    public DateTime? LastJoinedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }
    public int ReconnectionCount { get; set; }
    public int TotalDurationSeconds { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
