using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class NotebookFolderRepository : GenericRepository<NotebookFolder>, INotebookFolderRepository
    {
        public NotebookFolderRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<NotebookFolder>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
            => await _context.NotebookFolders
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<List<NotebookFolder>> SearchAsync(int userId, string pattern, int limit, CancellationToken cancellationToken = default)
            => await _context.NotebookFolders
                .Where(x => x.UserId == userId && !x.IsDeleted && EF.Functions.ILike(x.Name, pattern, "\\"))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Take(limit)
                .ToListAsync(cancellationToken);

        public async Task<NotebookFolder?> GetDefaultByUserIdAsync(int userId, CancellationToken cancellationToken = default)
            => await _context.NotebookFolders
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<NotebookFolder?> GetByIdForUpdateAsync(int folderId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.NotebookFolders
                    .FromSqlInterpolated($"SELECT * FROM \"NotebookFolders\" WHERE \"Id\" = {folderId} AND NOT \"IsDeleted\" FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return await _context.NotebookFolders
                    .FirstOrDefaultAsync(x => x.Id == folderId && !x.IsDeleted, cancellationToken);
            }
        }
    }
}
