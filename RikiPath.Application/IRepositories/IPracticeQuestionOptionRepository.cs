using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IPracticeQuestionOptionRepository : IGenericRepository<PracticeQuestionOption>
    {
        Task<List<PracticeQuestionOption>> GetByQuestionIdAsync(int practiceQuestionId);
    }
}
