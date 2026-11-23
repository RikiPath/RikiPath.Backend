using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LearnerNoteRepository : GenericRepository<LearnerNote>, ILearnerNoteRepository
    {
        public LearnerNoteRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<LearnerNote>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
            => await _context.LearnerNotes
                .Include(x => x.Folder)
                .Where(x => x.UserId == userId && !x.IsDeleted && (x.Folder == null || !x.Folder.IsDeleted))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<List<LearnerNote>> GetByFolderIdAsync(int userId, int folderId, CancellationToken cancellationToken = default)
            => await _context.LearnerNotes
                .Where(x => x.UserId == userId && x.FolderId == folderId && !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<List<LearnerNote>> SearchFilesAsync(int userId, string pattern, int limit, CancellationToken cancellationToken = default)
            => await _context.LearnerNotes
                .Include(x => x.Folder)
                .Where(x =>
                    x.UserId == userId &&
                    !x.IsDeleted &&
                    (x.Folder == null || !x.Folder.IsDeleted) &&
                    (EF.Functions.ILike(x.Name, pattern, "\\") || (x.Description != null && EF.Functions.ILike(x.Description, pattern, "\\"))))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Take(limit)
                .ToListAsync(cancellationToken);

        public async Task<LearnerNote?> GetDefaultByUserIdAsync(int userId, CancellationToken cancellationToken = default)
            => await _context.LearnerNotes
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<LearnerNote?> GetWithEntriesAsync(int vocabularyListId, CancellationToken cancellationToken = default)
            => await _context.LearnerNotes
                .Include(x => x.Folder)
                .Include(x => x.Pages!.Where(p => !p.IsDeleted))
                .Include(x => x.LearnerNoteEntries!.Where(e => !e.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == vocabularyListId && !x.IsDeleted, cancellationToken);

        public async Task<LearnerNote?> GetByIdForUpdateAsync(int fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.LearnerNotes
                    .FromSqlInterpolated($"SELECT * FROM \"LearnerNotes\" WHERE \"Id\" = {fileId} AND NOT \"IsDeleted\" FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return await _context.LearnerNotes
                    .FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted, cancellationToken);
            }
        }
    }
}
