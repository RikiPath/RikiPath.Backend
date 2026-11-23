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
    public class NotebookPageRepository : GenericRepository<NotebookPage>, INotebookPageRepository
    {
        public NotebookPageRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<NotebookPage>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
            => await _context.NotebookPages
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Where(x => x.UserId == userId && !x.IsDeleted && !x.LearnerNote.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.ModifiedDate ?? x.CreatedDate)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<List<NotebookPage>> GetByFileIdAsync(int userId, int fileId, CancellationToken cancellationToken = default)
            => await _context.NotebookPages
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Where(x => x.UserId == userId && x.LearnerNoteId == fileId && !x.IsDeleted && !x.LearnerNote.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<List<NotebookPage>> GetFavoritesByUserAsync(int userId, CancellationToken cancellationToken = default)
            => await _context.NotebookPages
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Where(x => x.UserId == userId && x.IsFavorite && !x.IsDeleted && !x.LearnerNote.IsDeleted && (x.LearnerNote.Folder == null || !x.LearnerNote.Folder.IsDeleted))
                .OrderByDescending(x => x.FavoritedAtUtc ?? x.ModifiedDate ?? x.CreatedDate)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<List<NotebookPage>> SearchPagesAsync(int userId, string pattern, int limit, CancellationToken cancellationToken = default)
            => await _context.NotebookPages
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Where(x =>
                    x.UserId == userId &&
                    !x.IsDeleted &&
                    !x.LearnerNote.IsDeleted &&
                    (x.LearnerNote.Folder == null || !x.LearnerNote.Folder.IsDeleted) &&
                    (EF.Functions.ILike(x.Title, pattern, "\\") || EF.Functions.ILike(x.ContentPlainText, pattern, "\\")))
                .OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate)
                .Take(limit)
                .ToListAsync(cancellationToken);

        public async Task<NotebookPage?> GetDefaultByUserIdAsync(int userId, CancellationToken cancellationToken = default)
            => await _context.NotebookPages
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Where(x => x.UserId == userId && !x.IsDeleted && !x.LearnerNote.IsDeleted)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<NotebookPage?> GetDetailedByIdAsync(int pageId, CancellationToken cancellationToken = default)
            => await _context.NotebookPages
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .FirstOrDefaultAsync(x => x.Id == pageId && !x.IsDeleted, cancellationToken);

        public async Task<NotebookPage?> GetByIdForUpdateAsync(int pageId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.NotebookPages
                    .FromSqlInterpolated($"SELECT * FROM \"NotebookPages\" WHERE \"Id\" = {pageId} AND NOT \"IsDeleted\" FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return await _context.NotebookPages
                    .FirstOrDefaultAsync(x => x.Id == pageId && !x.IsDeleted, cancellationToken);
            }
        }
    }
}
