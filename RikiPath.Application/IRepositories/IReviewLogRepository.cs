using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IReviewLogRepository : IGenericRepository<ReviewLog>
    {
        Task<List<ReviewLog>> GetByReviewItemIdAsync(int reviewItemId);
        Task<List<DateTime>> GetActivityDatesAsync(int userId);
    }
}
