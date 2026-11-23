using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface INotebookFolderRepository : IGenericRepository<NotebookFolder>
    {
        Task<List<NotebookFolder>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<NotebookFolder>> SearchAsync(int userId, string pattern, int limit, CancellationToken cancellationToken = default);
        Task<NotebookFolder?> GetDefaultByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<NotebookFolder?> GetByIdForUpdateAsync(int folderId, CancellationToken cancellationToken = default);
    }
}
