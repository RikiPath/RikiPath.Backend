using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeTestSectionResultRepository : IGenericRepository<PracticeTestSectionResult>
    {
        Task<List<PracticeTestSectionResult>> GetByAttemptIdAsync(int practiceTestAttemptId);
        Task<List<PracticeTestSectionResult>> GetSkillBreakdownAsync(int userId);
    }
}
