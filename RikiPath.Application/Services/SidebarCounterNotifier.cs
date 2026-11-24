using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Hubs;
using RikiPath.Application.IServices;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>
/// Best-effort SignalR trigger prompting affected users' frontends to refetch authoritative sidebar counters.
/// Never throws or aborts domain transactions.
/// </summary>
public sealed class SidebarCounterNotifier(
    IUnitOfWork unitOfWork,
    IHubContext<NotificationHub>? hubContext = null) : ISidebarCounterNotifier
{
    public async Task NotifyUsersAsync(
        IEnumerable<int> userIds,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (hubContext is null)
            return;

        var targets = userIds.Where(id => id > 0).Distinct().ToList();
        if (targets.Count == 0)
            return;

        var payload = new { Reason = reason, AtUtc = DateTime.UtcNow };
        foreach (var userId in targets)
        {
            try
            {
                await hubContext.Clients
                    .Group($"user_{userId}")
                    .SendAsync("SidebarCountersChanged", payload, cancellationToken);
            }
            catch
            {
                // Best-effort SignalR signal; fallback polling & focus refetch still maintain consistency.
            }
        }
    }

    public async Task NotifyAdminsAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (hubContext is null)
            return;

        try
        {
            var adminIds = await unitOfWork.UserAccounts.Query()
                .AsNoTracking()
                .Where(u => u.Role == Role.Admin && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            await NotifyUsersAsync(adminIds, reason, cancellationToken);
        }
        catch
        {
            // Best-effort SignalR signal.
        }
    }
}
