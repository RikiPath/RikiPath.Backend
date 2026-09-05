using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeQuestionRepository : IGenericRepository<PracticeQuestion>
    {
        Task<List<PracticeQuestion>> GetBySectionIdAsync(int practiceTestSectionId);
        Task<List<PracticeQuestion>> GetByIdsWithOptionsAsync(List<int> questionIds);
    }
}
