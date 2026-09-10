using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeTestAnswerRepository : IGenericRepository<PracticeTestAnswer>
    {
        Task<List<PracticeTestAnswer>> GetByAttemptIdAsync(int practiceTestAttemptId);
    }
}
