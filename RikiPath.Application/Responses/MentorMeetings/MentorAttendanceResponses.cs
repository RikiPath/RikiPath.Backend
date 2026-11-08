using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.MentorMeetings;

public sealed class MentorAttendanceResponse
{
    public int Id { get; set; }
    public int MentorBookingId { get; set; }
    public Guid RoomId { get; set; }
    public int UserId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public MentorAttendanceStatus Status { get; set; }
    public DateTime? FirstJoinedAtUtc { get; set; }
    public DateTime? LastJoinedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }
    public int ReconnectionCount { get; set; }
    public int TotalDurationSeconds { get; set; }
    public bool IsOnline { get; set; }
}

public sealed class ParticipantPresenceResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public int ActiveConnections { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
}

public sealed class RoomPresenceResponse
{
    public Guid RoomId { get; set; }
    public int TotalOnlineParticipants { get; set; }
    public List<ParticipantPresenceResponse> Participants { get; set; } = new();
}

public sealed class ParticipantJoinedEvent
{
    public Guid RoomId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "Joined";
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ParticipantLeftEvent
{
    public Guid RoomId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "Left";
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ParticipantReconnectedEvent
{
    public Guid RoomId { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int ReconnectionCount { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PresenceChangedEvent
{
    public Guid RoomId { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public int ActiveConnections { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MeetingStateChangedEvent
{
    public Guid RoomId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AttendanceUpdatedEvent
{
    public Guid RoomId { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public MentorAttendanceStatus Status { get; set; }
    public int ReconnectionCount { get; set; }
    public int TotalDurationSeconds { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RoomReadyEvent
{
    public Guid RoomId { get; set; }
    public bool IsReady { get; set; } = true;
    public int OnlineLearnersCount { get; set; }
    public bool IsMentorOnline { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RoomAccessCheckResponse
{
    public Guid RoomId { get; set; }
    public bool CanAccess { get; set; }
    public bool IsRoomReady { get; set; }
    public string? Reason { get; set; }
}
