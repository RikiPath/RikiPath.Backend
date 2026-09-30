using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILearnerNoteEntryRepository : IGenericRepository<LearnerNoteEntry>
    {
        /// <summary>Ownership is derived through LearnerNote (no direct UserId on this entity).</summary>
        Task<List<LearnerNoteEntry>> GetByUserAsync(int userId, int? vocabularyListId);
    }
}
