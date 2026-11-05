using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestSectionResultRepository : IGenericRepository<MockTestSectionResult>
    {
        Task<List<MockTestSectionResult>> GetByAttemptIdAsync(
            int practiceTestAttemptId,
            CancellationToken cancellationToken = default);
        Task<List<MockTestSectionResult>> GetLanguageSkillBreakdownAsync(int userId);
        Task<List<MockTestSectionResult>> GetSkillHistoryByUserSinceAsync(
            int userId,
            DateTime fromUtc,
            CancellationToken cancellationToken = default);
    }
}
