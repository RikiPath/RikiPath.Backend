namespace RikiPath.Application.IServices
{
    public interface IRealtimeNotifier
    {
        Task SendNotificationToUserAsync(int userId, string title, string message, string linkUrl, CancellationToken cancellationToken);
    }
}
