using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses.MentorMeetings;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace RikiPath.WebApi.Hubs;

/// <summary>
/// Phòng họp 1 mentor + nhiều learner (mesh P2P: mỗi người giữ 1 kết nối WebRTC tới từng người còn lại).
/// Peer được định danh bằng ConnectionId; signaling (offer/answer/ice) gửi ĐÍCH DANH tới 1 peer.
/// </summary>
[Authorize]
public sealed class MentorMeetingHub(IMentorMeetingAccessService meetingAccessService) : Hub
{
    // Mesh: 6 người => mỗi máy giữ 5 kết nối và upload 5 luồng video. Lớp đông hơn cần SFU (LiveKit, mediasoup...).
    private const int MaxLearners = 5;

    // Trạng thái nội bộ của hub (không phải DTO nên giữ lại ở đây)
    private sealed record Joined(Guid RoomId, int UserId, bool IsMentor, string Name);

    private static readonly ConcurrentDictionary<string, Joined> JoinedRooms = new();
    private static readonly Dictionary<Guid, string> Presenters = new(); // roomId -> connectionId đang chia sẻ màn hình
    private static readonly object RoomLock = new();

    // ===================== JOIN / LEAVE =====================

    public async Task<JoinMeetingRoomResponse> JoinRoom(Guid roomId, string displayName, string role)
    {
        // displayName / role từ client bị bỏ qua: tên và vai trò lấy từ DB
        var meeting = await meetingAccessService.GetMeetingAsync(roomId, Context.ConnectionAborted)
            ?? throw new HubException("Không tìm thấy phòng meeting đã thanh toán.");

        var userId = GetUserId();
        var isMentor = meeting.Mentor.UserId == userId;
        var learner = meeting.Learners.FirstOrDefault(l => l.UserId == userId);
        if (!isMentor && learner is null) throw new HubException("Bạn không có quyền vào phòng này.");
        var name = isMentor ? meeting.Mentor.Name : learner!.Name;

        // Connection này đang ở phòng khác -> rời phòng đó
        if (JoinedRooms.TryGetValue(Context.ConnectionId, out var prev) && prev.RoomId != roomId)
            await RemoveConnectionAsync(Context.ConnectionId);

        // F5 / mở tab mới: dọn connection cũ của chính user này trước (không tính vào sức chứa)
        var stale = JoinedRooms
            .Where(x => x.Value.RoomId == roomId && x.Value.UserId == userId && x.Key != Context.ConnectionId)
            .Select(x => x.Key)
            .ToList();
        foreach (var connId in stale)
            await RemoveConnectionAsync(connId);

        var me = new Joined(roomId, userId, isMentor, name);
        lock (RoomLock)
        {
            var learnerCount = JoinedRooms.Count(x => x.Value.RoomId == roomId && !x.Value.IsMentor && x.Key != Context.ConnectionId);
            if (!isMentor && learnerCount >= MaxLearners)
                throw new HubException($"Phòng đã đủ {MaxLearners} học viên.");
            JoinedRooms[Context.ConnectionId] = me;
        }

        var group = Group(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);

        var self = ToPeer(Context.ConnectionId, me);
        // Người đã ở trong phòng nhận UserJoined và là bên gửi offer cho người mới
        await Clients.OthersInGroup(group).SendAsync("UserJoined", self);

        var others = JoinedRooms
            .Where(x => x.Value.RoomId == roomId && x.Key != Context.ConnectionId)
            .Select(x => ToPeer(x.Key, x.Value))
            .ToList();

        return new JoinMeetingRoomResponse { Self = self, Participants = others };
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

    // ===================== WEBRTC SIGNALING (đích danh) =====================

    public Task SendOffer(Guid roomId, string targetConnectionId, string sdp)
    {
        var me = GetJoined(roomId);
        return SendToPeerAsync(roomId, targetConnectionId, "ReceiveOffer",
            Context.ConnectionId, sdp, ToPeer(Context.ConnectionId, me));
    }

    public Task SendAnswer(Guid roomId, string targetConnectionId, string sdp)
    {
        GetJoined(roomId);
        return SendToPeerAsync(roomId, targetConnectionId, "ReceiveAnswer", Context.ConnectionId, sdp);
    }

    public Task SendIceCandidate(Guid roomId, string targetConnectionId, System.Text.Json.JsonElement candidate)
    {
        GetJoined(roomId);
        return SendToPeerAsync(roomId, targetConnectionId, "ReceiveIceCandidate", Context.ConnectionId, candidate);
    }

    // ===================== CHAT / HAND / MEDIA STATE (broadcast) =====================

    // Dựng lại tin nhắn từ thông tin server biết, không chuyển tiếp nguyên object do client gửi
    public async Task SendChatMessage(Guid roomId, SendMeetingChatRequest request)
    {
        var me = GetJoined(roomId);
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveChatMessage", new
        {
            id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id,
            sender = me.Name,
            role = me.IsMentor ? "Mentor" : "Learner",
            text = text.Length > 2000 ? text[..2000] : text,
            timestamp = request.Timestamp,
        });
    }

    public async Task RaiseHand(Guid roomId, bool isRaised)
    {
        GetJoined(roomId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveHandRaised", Context.ConnectionId, isRaised);
    }

    public async Task ToggleMediaState(Guid roomId, MeetingMediaStateRequest state)
    {
        GetJoined(roomId);
        await Clients.OthersInGroup(Group(roomId)).SendAsync("ReceiveMediaState", Context.ConnectionId, state);
    }

    // ===================== CHIA SẺ MÀN HÌNH: mỗi lúc chỉ 1 người =====================

    public async Task StartPresenting(Guid roomId)
    {
        var me = GetJoined(roomId);
        string? previous = null;

        lock (RoomLock)
        {
            if (Presenters.TryGetValue(roomId, out var current) && current != Context.ConnectionId)
            {
                if (!me.IsMentor) throw new HubException("Đang có người khác chia sẻ màn hình.");
                previous = current; // mentor được quyền giành lại sân khấu
            }
            Presenters[roomId] = Context.ConnectionId;
        }

        if (previous is not null)
            await Clients.Client(previous).SendAsync("PresentationForceStopped");
    }

    public Task StopPresenting(Guid roomId)
    {
        GetJoined(roomId);
        lock (RoomLock)
        {
            if (Presenters.TryGetValue(roomId, out var current) && current == Context.ConnectionId)
                Presenters.Remove(roomId);
        }
        return Task.CompletedTask;
    }

    // ===================== QUYỀN MENTOR =====================

    public async Task MuteParticipant(Guid roomId, string targetConnectionId)
    {
        var me = GetJoined(roomId);
        if (!me.IsMentor) throw new HubException("Chỉ mentor mới được tắt mic người khác.");
        if (IsPeerInRoom(roomId, targetConnectionId))
            await Clients.Client(targetConnectionId).SendAsync("ForceMuted");
    }

    // ===================== HELPERS =====================

    private async Task RemoveConnectionAsync(string connectionId)
    {
        if (!JoinedRooms.TryRemove(connectionId, out var j)) return;

        lock (RoomLock)
        {
            if (Presenters.TryGetValue(j.RoomId, out var p) && p == connectionId)
                Presenters.Remove(j.RoomId);
        }

        await Groups.RemoveFromGroupAsync(connectionId, Group(j.RoomId));
        await Clients.Group(Group(j.RoomId)).SendAsync("UserLeft", connectionId);
    }

    private Joined GetJoined(Guid roomId)
        => JoinedRooms.TryGetValue(Context.ConnectionId, out var j) && j.RoomId == roomId
            ? j
            : throw new HubException("Bạn chưa tham gia phòng meeting này.");

    private static bool IsPeerInRoom(Guid roomId, string connectionId)
        => JoinedRooms.TryGetValue(connectionId, out var t) && t.RoomId == roomId;

    // Peer đã rời giữa chừng (ICE đến muộn...) thì bỏ qua, không ném lỗi
    private Task SendToPeerAsync(Guid roomId, string targetConnectionId, string method, params object?[] args)
        => targetConnectionId != Context.ConnectionId && IsPeerInRoom(roomId, targetConnectionId)
            ? Clients.Client(targetConnectionId).SendCoreAsync(method, args)
            : Task.CompletedTask;

    private static MeetingPeerResponse ToPeer(string connectionId, Joined j) => new()
    {
        ConnectionId = connectionId,
        Id = j.UserId,
        Name = j.Name,
        Role = j.IsMentor ? "Mentor" : "Learner",
    };

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