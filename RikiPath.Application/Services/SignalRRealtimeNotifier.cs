using Microsoft.AspNetCore.SignalR;
using RikiPath.Application.Hubs;
using RikiPath.Application.IServices;

namespace RikiPath.Application.Services
{
    public class SignalRRealtimeNotifier(IHubContext<NotificationHub> hubContext) : IRealtimeNotifier
    {
        public async Task SendNotificationToUserAsync(int userId, string title, string message, string linkUrl, CancellationToken cancellationToken)
        {
            // Broadcast to the exact SignalR group joined in NotificationHub.OnConnectedAsync (`user_{userId}`).
            await hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
            {
                Title = title,
                Message = message,
                LinkUrl = linkUrl,
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
    }
}
