using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface INotebookPageRepository : IGenericRepository<NotebookPage>
    {
        Task<List<NotebookPage>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<NotebookPage>> GetByFileIdAsync(int userId, int fileId, CancellationToken cancellationToken = default);
        Task<List<NotebookPage>> GetFavoritesByUserAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<NotebookPage>> SearchPagesAsync(int userId, string pattern, int limit, CancellationToken cancellationToken = default);
        Task<NotebookPage?> GetDefaultByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<NotebookPage?> GetDetailedByIdAsync(int pageId, CancellationToken cancellationToken = default);
        Task<NotebookPage?> GetByIdForUpdateAsync(int pageId, CancellationToken cancellationToken = default);
    }
}
