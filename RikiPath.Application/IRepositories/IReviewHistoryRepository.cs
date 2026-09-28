using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IReviewHistoryRepository : IGenericRepository<ReviewHistory>
    {
        Task<List<ReviewHistory>> GetByReviewCardIdAsync(int reviewItemId);
        Task<List<DateTime>> GetActivityDatesAsync(int userId);
    }
}
