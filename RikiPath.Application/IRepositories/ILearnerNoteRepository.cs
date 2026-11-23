using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILearnerNoteRepository : IGenericRepository<LearnerNote>
    {
        Task<List<LearnerNote>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<LearnerNote>> GetByFolderIdAsync(int userId, int folderId, CancellationToken cancellationToken = default);
        Task<List<LearnerNote>> SearchFilesAsync(int userId, string pattern, int limit, CancellationToken cancellationToken = default);
        Task<LearnerNote?> GetDefaultByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<LearnerNote?> GetWithEntriesAsync(int vocabularyListId, CancellationToken cancellationToken = default);
        Task<LearnerNote?> GetByIdForUpdateAsync(int fileId, CancellationToken cancellationToken = default);
    }
}
