using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
    {
        public NotificationRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(List<Notification> Items, int TotalCount)> GetByUserAsync(
            int userId, bool unreadOnly, int page, int pageSize)
        {
            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(n => n.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CountUnreadAsync(int userId)
            => await _context.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead);

        public async Task MarkAllAsReadAsync(int userId, DateTime? asOfUtc = null)
        {
            var query = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead);
            if (asOfUtc.HasValue)
            {
                var cutoff = asOfUtc.Value;
                query = query.Where(n => n.CreatedDate == null || n.CreatedDate <= cutoff);
            }

            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                var unreadList = await query.ToListAsync();
                foreach (var n in unreadList)
                {
                    n.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }
            else
            {
                await query.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            }
        }
    }
}
