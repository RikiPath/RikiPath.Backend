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
    public class LearnerNoteEntryRepository : GenericRepository<LearnerNoteEntry>, ILearnerNoteEntryRepository
    {
        public LearnerNoteEntryRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<LearnerNoteEntry>> GetByUserAsync(int userId, int? vocabularyListId, CancellationToken cancellationToken = default)
        {
            IQueryable<LearnerNoteEntry> query = _context.LearnerNoteEntries
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Include(x => x.NotebookPage)
                .Include(x => x.Vocabulary)
                .Include(x => x.Kanji)
                .Include(x => x.GrammarPattern)
                .Include(x => x.ReviewCards!.Where(card => !card.IsDeleted))
                .Where(x =>
                    !x.IsDeleted &&
                    x.LearnerNote.UserId == userId &&
                    !x.LearnerNote.IsDeleted &&
                    (x.NotebookPage == null || !x.NotebookPage.IsDeleted) &&
                    (x.LearnerNote.Folder == null || !x.LearnerNote.Folder.IsDeleted));

            if (vocabularyListId.HasValue)
                query = query.Where(x => x.LearnerNoteId == vocabularyListId.Value);

            return await query
                .OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<LearnerNoteEntry>> GetByPageIdAsync(int userId, int notebookPageId, CancellationToken cancellationToken = default)
            => await _context.LearnerNoteEntries
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Include(x => x.NotebookPage)
                .Include(x => x.Vocabulary)
                .Include(x => x.Kanji)
                .Include(x => x.GrammarPattern)
                .Include(x => x.ReviewCards!.Where(card => !card.IsDeleted))
                .Where(x =>
                    !x.IsDeleted &&
                    x.NotebookPageId == notebookPageId &&
                    x.LearnerNote.UserId == userId &&
                    !x.LearnerNote.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<(List<LearnerNoteEntry> Items, int TotalCount)> QuerySavedItemsAsync(
            int userId,
            string? likePattern,
            string? kind,
            int? pageId,
            int? fileId,
            int? folderId,
            bool? favoriteOnly,
            string? sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.LearnerNoteEntries
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Include(x => x.NotebookPage)
                .Include(x => x.Vocabulary)
                .Include(x => x.Kanji)
                .Include(x => x.GrammarPattern)
                .Include(x => x.ReviewCards!.Where(card => !card.IsDeleted))
                .Where(x =>
                    !x.IsDeleted &&
                    x.LearnerNote.UserId == userId &&
                    !x.LearnerNote.IsDeleted &&
                    (x.NotebookPage == null || !x.NotebookPage.IsDeleted) &&
                    (x.LearnerNote.Folder == null || !x.LearnerNote.Folder.IsDeleted));

            if (pageId.HasValue)
                query = query.Where(x => x.NotebookPageId == pageId.Value);
            if (fileId.HasValue)
                query = query.Where(x => x.LearnerNoteId == fileId.Value);
            if (folderId.HasValue)
                query = query.Where(x => x.LearnerNote.FolderId == folderId.Value);
            if (favoriteOnly == true)
                query = query.Where(x => x.IsFavorite);

            var cleanKind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(cleanKind) && cleanKind != "all")
            {
                query = cleanKind switch
                {
                    "vocabulary" => query.Where(x => x.VocabularyId.HasValue || x.ItemKind == "vocabulary"),
                    "kanji" => query.Where(x => x.KanjiId.HasValue || x.ItemKind == "kanji"),
                    "grammar" => query.Where(x => x.GrammarPatternId.HasValue || x.ItemKind == "grammar"),
                    _ => query.Where(x => x.ItemKind == cleanKind),
                };
            }

            if (!string.IsNullOrWhiteSpace(likePattern))
            {
                query = query.Where(x =>
                    (x.ManualWord != null && EF.Functions.ILike(x.ManualWord, likePattern, "\\")) ||
                    (x.ManualReading != null && EF.Functions.ILike(x.ManualReading, likePattern, "\\")) ||
                    (x.ManualMeaning != null && EF.Functions.ILike(x.ManualMeaning, likePattern, "\\")) ||
                    (x.Note != null && EF.Functions.ILike(x.Note, likePattern, "\\")) ||
                    (x.ContextSnippet != null && EF.Functions.ILike(x.ContextSnippet, likePattern, "\\")) ||
                    (x.SourceTitle != null && EF.Functions.ILike(x.SourceTitle, likePattern, "\\")) ||
                    (x.Vocabulary != null && (EF.Functions.ILike(x.Vocabulary.Word, likePattern, "\\") || EF.Functions.ILike(x.Vocabulary.Reading, likePattern, "\\") || EF.Functions.ILike(x.Vocabulary.Meaning, likePattern, "\\"))) ||
                    (x.Kanji != null && (EF.Functions.ILike(x.Kanji.Character, likePattern, "\\") || EF.Functions.ILike(x.Kanji.Meaning, likePattern, "\\") || (x.Kanji.OnYomi != null && EF.Functions.ILike(x.Kanji.OnYomi, likePattern, "\\")) || (x.Kanji.KunYomi != null && EF.Functions.ILike(x.Kanji.KunYomi, likePattern, "\\")))) ||
                    (x.GrammarPattern != null && (EF.Functions.ILike(x.GrammarPattern.Title, likePattern, "\\") || EF.Functions.ILike(x.GrammarPattern.Structure, likePattern, "\\"))));
            }

            var cleanSort = (sortBy ?? "recent").Trim().ToLowerInvariant();
            query = cleanSort switch
            {
                "alpha" => query.OrderBy(x => x.Vocabulary != null ? x.Vocabulary.Word : x.Kanji != null ? x.Kanji.Character : x.GrammarPattern != null ? x.GrammarPattern.Title : x.ManualWord ?? string.Empty).ThenBy(x => x.Id),
                "srs" => query.OrderByDescending(x => x.ReviewCards!.Any(c => !c.IsDeleted)).ThenByDescending(x => x.ModifiedDate ?? x.CreatedDate).ThenByDescending(x => x.Id),
                _ => query.OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate).ThenByDescending(x => x.Id),
            };

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<LearnerNoteEntry?> GetDetailedByIdAsync(int entryId, CancellationToken cancellationToken = default)
            => await _context.LearnerNoteEntries
                .Include(x => x.LearnerNote)
                    .ThenInclude(f => f.Folder)
                .Include(x => x.NotebookPage)
                .Include(x => x.Vocabulary)
                .Include(x => x.Kanji)
                .Include(x => x.GrammarPattern)
                .Include(x => x.ReviewCards!.Where(card => !card.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == entryId && !x.IsDeleted, cancellationToken);

        public async Task<LearnerNoteEntry?> GetByIdForUpdateAsync(int entryId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.LearnerNoteEntries
                    .FromSqlInterpolated($"SELECT * FROM \"LearnerNoteEntries\" WHERE \"Id\" = {entryId} AND NOT \"IsDeleted\" FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return await _context.LearnerNoteEntries
                    .FirstOrDefaultAsync(x => x.Id == entryId && !x.IsDeleted, cancellationToken);
            }
        }
    }
}
