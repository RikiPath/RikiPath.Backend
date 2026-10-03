using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.IServices;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace RikiPath.WebApi.Hubs;

[Authorize]
public sealed class MentorMeetingHub(IMentorMeetingAccessService meetingAccessService) : Hub
{
    private sealed record Joined(Guid RoomId, int UserId);
    private static readonly ConcurrentDictionary<string, Joined> JoinedRooms = new();

    private sealed record ParticipantInfo(int Id, string Name, string Role);

    public sealed record ChatMessageDto(string? Id, string? Text, string? Timestamp);

    public async Task JoinRoom(Guid roomId, string displayName, string role)
    {
        var meeting = await meetingAccessService.GetMeetingAsync(roomId, Context.ConnectionAborted);
        if (meeting is null) throw new HubException("Không tìm thấy phòng meeting đã thanh toán.");

        var userId = GetUserId();
        var isMentor = meeting.Mentor.UserId == userId;
        var isLearner = meeting.Learner.UserId == userId;
        if (!isLearner && !isMentor) throw new HubException("Bạn không có quyền vào phòng này.");

        // Connection này đang ở phòng khác -> rời phòng đó
        if (JoinedRooms.TryGetValue(Context.ConnectionId, out var prev) && prev.RoomId != roomId)
            await RemoveConnectionAsync(Context.ConnectionId);

        // F5 / mở tab mới: dọn connection cũ của chính user này TRƯỚC khi connection mới vào group,
        // để bên kia nhận UserLeft (reset peer) rồi mới nhận UserJoined (offer mới).
        var stale = JoinedRooms
            .Where(x => x.Value.RoomId == roomId && x.Value.UserId == userId && x.Key != Context.ConnectionId)
            .Select(x => x.Key)
            .ToList();
        foreach (var connId in stale)
            await RemoveConnectionAsync(connId);

        var room = Group(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        JoinedRooms[Context.ConnectionId] = new Joined(roomId, userId);

        await Clients.OthersInGroup(room).SendAsync("UserJoined", BuildUserInfo(meeting, isMentor));
    }

    public async Task SendOffer(Guid roomId, string sdp)
    {
        var (userId, meeting) = await ValidateAndGetAsync(roomId);
        var senderInfo = BuildUserInfo(meeting, meeting.Mentor.UserId == userId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveOffer", userId, sdp, senderInfo);
    }

    public async Task SendAnswer(Guid roomId, string sdp)
    {
        var (userId, _) = await ValidateAndGetAsync(roomId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveAnswer", userId, sdp);
    }

    public async Task SendIceCandidate(Guid roomId, System.Text.Json.JsonElement candidate)
    {
        var (userId, _) = await ValidateAndGetAsync(roomId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveIceCandidate", userId, candidate);
    }

    // Dựng lại tin nhắn từ thông tin server biết, không chuyển tiếp nguyên object do client gửi
    public async Task SendChatMessage(Guid roomId, ChatMessageDto message)
    {
        var (userId, meeting) = await ValidateAndGetAsync(roomId);
        var text = message.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var info = BuildUserInfo(meeting, meeting.Mentor.UserId == userId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveChatMessage", new
        {
            id = string.IsNullOrWhiteSpace(message.Id) ? Guid.NewGuid().ToString("N") : message.Id,
            sender = info.Name,
            role = info.Role,
            text = text.Length > 2000 ? text[..2000] : text,
            timestamp = message.Timestamp,
        });
    }

    public async Task RaiseHand(Guid roomId, bool isRaised)
    {
        var (userId, meeting) = await ValidateAndGetAsync(roomId);
        var info = BuildUserInfo(meeting, meeting.Mentor.UserId == userId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveHandRaised", new { userName = info.Name, isRaised });
    }

    // Client lắng nghe "ReceiveMediaState" để hiện mic/cam/đang share màn hình của đối phương
    public async Task ToggleMediaState(Guid roomId, object state)
    {
        var (userId, _) = await ValidateAndGetAsync(roomId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveMediaState", userId, state);
    }

    public async Task LeaveRoom(Guid roomId)
    {
        if (JoinedRooms.TryGetValue(Context.ConnectionId, out var joined) && joined.RoomId == roomId)
            await RemoveConnectionAsync(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Gỡ connection khỏi phòng; chỉ báo UserLeft khi user đó không còn connection nào khác trong phòng
    private async Task RemoveConnectionAsync(string connectionId)
    {
        if (!JoinedRooms.TryRemove(connectionId, out var j)) return;

        await Groups.RemoveFromGroupAsync(connectionId, Group(j.RoomId));

        var stillIn = JoinedRooms.Values.Any(x => x.RoomId == j.RoomId && x.UserId == j.UserId);
        if (!stillIn)
            await Clients.Group(Group(j.RoomId)).SendAsync("UserLeft", j.UserId);
    }

    private async Task<(int UserId, MeetingAccessInfoResponse Meeting)> ValidateAndGetAsync(Guid roomId)
    {
        if (!JoinedRooms.TryGetValue(Context.ConnectionId, out var joined) || joined.RoomId != roomId)
            throw new HubException("Bạn chưa tham gia phòng meeting này.");

        var meeting = await meetingAccessService.GetMeetingAsync(roomId, Context.ConnectionAborted);
        var userId = GetUserId();
        if (meeting is null || !CanJoin(meeting, userId))
            throw new HubException("Quyền truy cập phòng đã hết hiệu lực.");

        return (userId, meeting);
    }

    private static bool CanJoin(MeetingAccessInfoResponse meeting, int userId)
        => meeting.Learner.UserId == userId || meeting.Mentor.UserId == userId;

    // Không tin displayName/role FE gửi lên: tên thật lấy từ DB qua MentorMeetingAccessService
    private static ParticipantInfo BuildUserInfo(MeetingAccessInfoResponse meeting, bool isMentor)
    {
        var p = isMentor ? meeting.Mentor : meeting.Learner;
        return new ParticipantInfo(p.UserId, p.Name, isMentor ? "Mentor" : "Learner");
    }

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