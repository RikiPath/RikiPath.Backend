namespace RikiPath.Application.IServices;

public interface ISidebarCounterNotifier
{
    Task NotifyUsersAsync(IEnumerable<int> userIds, string reason, CancellationToken cancellationToken = default);
    Task NotifyAdminsAsync(string reason, CancellationToken cancellationToken = default);
}
