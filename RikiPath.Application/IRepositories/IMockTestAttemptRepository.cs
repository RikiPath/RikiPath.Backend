using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestAttemptRepository : IGenericRepository<MockTestAttempt>
    {
        Task<List<MockTestAttempt>> GetByUserAsync(int userId);
        Task<MockTestAttempt?> GetWithBreakdownAsync(int attemptId);
        Task<List<DateTime>> GetActivityDatesAsync(int userId);
    }
}
