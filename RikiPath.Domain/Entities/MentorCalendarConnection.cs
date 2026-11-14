namespace RikiPath.Domain.Entities;

public sealed class MentorCalendarConnection
{
    public int Id { get; set; }
    public int MentorId { get; set; }
    public UserAccount Mentor { get; set; } = null!;
    public string? ProtectedRefreshToken { get; set; }
    public string? StateHash { get; set; }
    public DateTime? StateExpiresAt { get; set; }
    public string SyncedEventsJson { get; set; } = "{}";
    public DateTime? LastSyncedAt { get; set; }
}
