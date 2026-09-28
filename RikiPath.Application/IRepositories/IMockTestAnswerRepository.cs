using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockTestAnswerRepository : IGenericRepository<MockTestAnswer>
    {
        Task<List<MockTestAnswer>> GetByAttemptIdAsync(int practiceTestAttemptId);
    }
}
