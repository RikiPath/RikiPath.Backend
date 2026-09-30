using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILearnerNoteRepository : IGenericRepository<LearnerNote>
    {
        Task<List<LearnerNote>> GetByUserIdAsync(int userId);
        Task<LearnerNote?> GetWithEntriesAsync(int vocabularyListId);
    }
}
