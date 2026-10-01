using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.WebApi.Hubs;

/// <summary>Authenticated WebRTC signaling for paid MentorMeeting rooms. Media is peer-to-peer.</summary>
[Authorize]
public sealed class MentorMeetingHub(AppDbContext db) : Hub
{
    private static readonly ConcurrentDictionary<string, Guid> JoinedRooms = new();

    public async Task JoinRoom(Guid roomId)
    {
        var meeting = await GetMeetingAsync(roomId);
        if (meeting is null) throw new HubException("Không tìm thấy phòng meeting đã thanh toán.");

        var userId = GetUserId();
        var isLearner = meeting.UserSubscription.UserId == userId;
        var isMentor = meeting.MentorAvailability?.MentorId == userId
            && meeting.MentorAvailability.Mentor?.Role == Role.Mentor;
        if (!isLearner && !isMentor) throw new HubException("Bạn không có quyền vào phòng này.");

        if (JoinedRooms.TryGetValue(Context.ConnectionId, out var previousRoom) && previousRoom != roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(previousRoom));
            await Clients.OthersInGroup(Group(previousRoom)).SendAsync("ParticipantLeft", new { UserId = userId });
        }
        var room = Group(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        JoinedRooms[Context.ConnectionId] = roomId;
        await Clients.OthersInGroup(room).SendAsync("ParticipantJoined", new
        {
            UserId = userId,
            Role = isMentor ? "Mentor" : "Learner"
        });
    }

    public Task SendOffer(Guid roomId, string sdp) => RelayToPeer(roomId, "ReceiveOffer", sdp);
    public Task SendAnswer(Guid roomId, string sdp) => RelayToPeer(roomId, "ReceiveAnswer", sdp);
    public Task SendIceCandidate(Guid roomId, System.Text.Json.JsonElement candidate)
        => RelayToPeer(roomId, "ReceiveIceCandidate", candidate);

    public async Task LeaveRoom(Guid roomId)
    {
        if (JoinedRooms.TryGetValue(Context.ConnectionId, out var joined) && joined == roomId)
        {
            JoinedRooms.TryRemove(Context.ConnectionId, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(roomId));
            await Clients.OthersInGroup(Group(roomId)).SendAsync("ParticipantLeft", new { UserId = GetUserId() });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (JoinedRooms.TryRemove(Context.ConnectionId, out var roomId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(roomId));
            await Clients.Group(Group(roomId)).SendAsync("ParticipantLeft", new { UserId = TryGetUserId() });
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task RelayToPeer(Guid roomId, string eventName, object payload)
    {
        if (!JoinedRooms.TryGetValue(Context.ConnectionId, out var joined) || joined != roomId)
            throw new HubException("Bạn chưa tham gia phòng meeting này.");
        var meeting = await GetMeetingAsync(roomId);
        if (meeting is null || !CanJoin(meeting, GetUserId()))
            throw new HubException("Quyền truy cập phòng đã hết hiệu lực.");

        await Clients.OthersInGroup(Group(roomId)).SendAsync(eventName, new
        {
            UserId = GetUserId(),
            Payload = payload
        });
    }

    private Task<RikiPath.Domain.Entities.MentorBooking?> GetMeetingAsync(Guid roomId)
        => db.MentorBookings
            .Include(x => x.UserSubscription)
            .Include(x => x.MentorAvailability).ThenInclude(x => x!.Mentor)
            .FirstOrDefaultAsync(x => x.RoomId == roomId && !x.IsDeleted
                && x.UserSubscription.PaymentStatus == PaymentStatus.Paid
                && (x.Status == ConsultationStatus.Assigned || x.Status == ConsultationStatus.Accepted));

    private static bool CanJoin(RikiPath.Domain.Entities.MentorBooking meeting, int userId)
        => meeting.UserSubscription.UserId == userId
            || (meeting.MentorAvailability?.MentorId == userId
                && meeting.MentorAvailability.Mentor?.Role == Role.Mentor);

    private int GetUserId()
        => TryGetUserId() is int userId && userId > 0
            ? userId
            : throw new HubException("Không tìm thấy UserId trong access token.");

    private int? TryGetUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue("userId")
            ?? Context.User?.FindFirstValue("id");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static string Group(Guid roomId) => $"mentor-meeting:{roomId:N}";
}
