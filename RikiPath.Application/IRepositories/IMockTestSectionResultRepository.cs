using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestSectionResultRepository : IGenericRepository<MockTestSectionResult>
    {
        Task<List<MockTestSectionResult>> GetByAttemptIdAsync(int practiceTestAttemptId);
        Task<List<MockTestSectionResult>> GetLanguageSkillBreakdownAsync(int userId);
    }
}
