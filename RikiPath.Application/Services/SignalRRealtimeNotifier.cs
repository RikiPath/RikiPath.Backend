using Microsoft.AspNetCore.SignalR;
using RikiPath.Application.Hubs;
using RikiPath.Application.IServices;

namespace RikiPath.Application.Services
{
    public class SignalRRealtimeNotifier(IHubContext<NotificationHub> hubContext) : IRealtimeNotifier
    {
        public async Task SendNotificationToUserAsync(int userId, string title, string message, string linkUrl, CancellationToken cancellationToken)
        {
            // Gửi tới đúng user có UserId
            await hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
            {
                Title = title,
                Message = message,
                LinkUrl = linkUrl
            }, cancellationToken);
        }
    }
}
