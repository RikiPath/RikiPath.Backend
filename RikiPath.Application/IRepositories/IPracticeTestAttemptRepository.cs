using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeTestAttemptRepository : IGenericRepository<PracticeTestAttempt>
    {
        Task<List<PracticeTestAttempt>> GetByUserAsync(int userId);
        Task<PracticeTestAttempt?> GetWithBreakdownAsync(int attemptId);
        Task<List<DateTime>> GetActivityDatesAsync(int userId);
    }
}
