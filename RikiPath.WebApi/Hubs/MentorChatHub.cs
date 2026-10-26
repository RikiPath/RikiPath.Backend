using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;

namespace RikiPath.WebApi.Hubs;

/// <summary>Private realtime conversation per learner/mentor; booking IDs resolve to one canonical group.</summary>
[Authorize(Roles = "Learner,Mentor")]
public sealed class MentorChatHub(IMentorChatService chatService) : Hub
{
    public async Task JoinBookingChat(int bookingId)
    {
        var response = await chatService.GetThreadAsync(bookingId, Context.ConnectionAborted);
        if (!response.IsSuccess)
            throw new HubException(response.ErrorMessage ?? "Bạn không có quyền truy cập cuộc trò chuyện này.");
        var requestedId = bookingId;
        bookingId = response.Result!.BookingId;
        Context.Items[ContextKey(requestedId)] = bookingId;

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(bookingId), Context.ConnectionAborted);
        Context.Items[ContextKey(bookingId)] = bookingId;
    }

    public async Task LeaveBookingChat(int bookingId)
    {
        bookingId = Context.Items.TryGetValue(ContextKey(bookingId), out var canonical) && canonical is int id ? id : bookingId;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(bookingId), Context.ConnectionAborted);
        foreach (var key in Context.Items.Where(x => x.Key is string name && name.StartsWith("mentor-chat-joined:")
            && x.Value is int conversationId && conversationId == bookingId).Select(x => x.Key).ToList())
            Context.Items.Remove(key);
    }

    public async Task SendChatMessage(int bookingId, SendMentorChatMessageRequest request)
    {
        EnsureJoined(bookingId);
        var response = await chatService.SendAsync(bookingId, request.Text, Context.ConnectionAborted);
        if (!response.IsSuccess || response.Result is null)
            throw new HubException(response.ErrorMessage ?? "Không gửi được tin nhắn.");

        await Clients.Group(Group(response.Result.BookingId)).SendAsync(
            "ReceiveMentorChatMessage", response.Result, Context.ConnectionAborted);
    }

    public async Task MarkRead(int bookingId)
    {
        EnsureJoined(bookingId);
        var response = await chatService.MarkReadAsync(bookingId, Context.ConnectionAborted);
        if (!response.IsSuccess)
            throw new HubException(response.ErrorMessage ?? "Không thể cập nhật trạng thái đã đọc.");

        bookingId = (int)Context.Items[ContextKey(bookingId)]!;
        await Clients.OthersInGroup(Group(bookingId)).SendAsync(
            "MentorChatRead", bookingId, Context.ConnectionAborted);
    }

    private void EnsureJoined(int bookingId)
    {
        if (!Context.Items.ContainsKey(ContextKey(bookingId)))
            throw new HubException("Hãy tham gia cuộc trò chuyện trước khi gửi tin.");
    }

    private static string ContextKey(int bookingId) => $"mentor-chat-joined:{bookingId}";
    private static string Group(int bookingId) => $"mentor-chat:{bookingId}";
}
