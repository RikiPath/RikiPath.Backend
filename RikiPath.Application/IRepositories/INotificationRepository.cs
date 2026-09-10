using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface INotificationRepository : IGenericRepository<Notification>
    {
        Task<(List<Notification> Items, int TotalCount)> GetByUserAsync(int userId, bool unreadOnly, int page, int pageSize);
        Task<int> CountUnreadAsync(int userId);
        Task MarkAllAsReadAsync(int userId);
    }
}
