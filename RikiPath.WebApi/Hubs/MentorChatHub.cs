using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;

namespace RikiPath.WebApi.Hubs;

/// <summary>Private realtime chat scoped to one paid MentorBooking, separate from meeting-room chat.</summary>
[Authorize(Roles = "Learner,Mentor")]
public sealed class MentorChatHub(IMentorChatService chatService) : Hub
{
    public async Task JoinBookingChat(int bookingId)
    {
        var response = await chatService.GetThreadAsync(bookingId, Context.ConnectionAborted);
        if (!response.IsSuccess)
            throw new HubException(response.ErrorMessage ?? "Bạn không có quyền truy cập cuộc trò chuyện này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(bookingId), Context.ConnectionAborted);
        Context.Items[ContextKey(bookingId)] = true;
    }

    public async Task LeaveBookingChat(int bookingId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(bookingId), Context.ConnectionAborted);
        Context.Items.Remove(ContextKey(bookingId));
    }

    public async Task SendChatMessage(int bookingId, SendMentorChatMessageRequest request)
    {
        EnsureJoined(bookingId);
        var response = await chatService.SendAsync(bookingId, request.Text, Context.ConnectionAborted);
        if (!response.IsSuccess || response.Result is null)
            throw new HubException(response.ErrorMessage ?? "Không gửi được tin nhắn.");

        await Clients.Group(Group(bookingId)).SendAsync(
            "ReceiveMentorChatMessage", response.Result, Context.ConnectionAborted);
    }

    public async Task MarkRead(int bookingId)
    {
        EnsureJoined(bookingId);
        var response = await chatService.MarkReadAsync(bookingId, Context.ConnectionAborted);
        if (!response.IsSuccess)
            throw new HubException(response.ErrorMessage ?? "Không thể cập nhật trạng thái đã đọc.");

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
