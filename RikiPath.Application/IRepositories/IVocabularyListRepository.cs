using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IVocabularyListRepository : IGenericRepository<VocabularyList>
    {
        Task<List<VocabularyList>> GetByUserIdAsync(int userId);
        Task<VocabularyList?> GetWithEntriesAsync(int vocabularyListId);
    }
}
