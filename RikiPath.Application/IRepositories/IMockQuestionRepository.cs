using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockQuestionRepository : IGenericRepository<MockQuestion>
    {
        Task<List<MockQuestion>> GetBySectionIdAsync(int practiceTestSectionId);
        Task<List<MockQuestion>> GetByIdsWithOptionsAsync(List<int> questionIds);
    }
}
