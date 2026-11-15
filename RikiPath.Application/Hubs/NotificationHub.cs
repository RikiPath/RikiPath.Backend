using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RikiPath.Application.Hubs;

[Authorize]
public class NotificationHub(IUnitOfWork? unitOfWork = null) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = TryGetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
            if (unitOfWork is not null)
            {
                var unreadCount = await unitOfWork.Notifications.CountUnreadAsync(userId.Value);
                await Clients.Caller.SendAsync("UnreadCountChanged", unreadCount);
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = TryGetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task RequestSync()
    {
        var userId = TryGetUserId();
        if (userId.HasValue && unitOfWork is not null)
        {
            var unreadCount = await unitOfWork.Notifications.CountUnreadAsync(userId.Value);
            await Clients.Caller.SendAsync("UnreadCountChanged", unreadCount);
        }
    }

    private int? TryGetUserId()
    {
        var raw = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst("userId")?.Value
            ?? Context.User?.FindFirst("id")?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }
}
