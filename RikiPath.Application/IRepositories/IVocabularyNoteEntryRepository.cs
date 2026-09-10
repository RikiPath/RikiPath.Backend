using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IVocabularyNoteEntryRepository : IGenericRepository<VocabularyNoteEntry>
    {
        /// <summary>Ownership is derived through VocabularyList (no direct UserId on this entity).</summary>
        Task<List<VocabularyNoteEntry>> GetByUserAsync(int userId, int? vocabularyListId);
    }
}
