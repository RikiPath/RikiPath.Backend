using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMockQuestionOptionRepository : IGenericRepository<MockQuestionOption>
    {
        Task<List<MockQuestionOption>> GetByQuestionIdAsync(int practiceQuestionId);
    }
}
