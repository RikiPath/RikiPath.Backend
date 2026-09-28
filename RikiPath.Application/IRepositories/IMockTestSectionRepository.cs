using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestSectionRepository : IGenericRepository<MockTestSection>
    {
        Task<List<MockTestSection>> GetByMockTestIdAsync(int practiceTestId);
    }
}
