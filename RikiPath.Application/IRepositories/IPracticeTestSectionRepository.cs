using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeTestSectionRepository : IGenericRepository<PracticeTestSection>
    {
        Task<List<PracticeTestSection>> GetByPracticeTestIdAsync(int practiceTestId);
    }
}
