using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class VocabularyEntryRepository : GenericRepository<VocabularyEntry>, IVocabularyEntryRepository
    {
        public VocabularyEntryRepository(AppDbContext context) : base(context)
        {
        }

        private IQueryable<VocabularyEntry> BuildSearchQuery(int? jlptLevelId, ContentStatus? status, string? keyword)
        {
            IQueryable<VocabularyEntry> query = _context.VocabularyEntries.AsQueryable();
            if (jlptLevelId.HasValue) query = query.Where(x => x.JlptLevelId == jlptLevelId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(x => x.Word.Contains(keyword) || x.Reading.Contains(keyword) || x.Meaning.ToLower().Contains(keyword.ToLower()));
            return query;
        }

        public async Task<(List<VocabularyEntry> items, int total)> SearchAsync(
             int? jlptLevelId,
             ContentStatus? status,
             string? keyword,
             int pageIndex,
             int pageSize)
        {
            var query = BuildSearchQuery(jlptLevelId, status, keyword);

            var total = await query.CountAsync();

            var items = await query
                .Include(x => x.JlptLevel)
                .OrderBy(x => x.Word)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, status, keyword).CountAsync();

        public async Task<List<VocabularyEntry>> GetPendingReviewAsync()
            => await _context.VocabularyEntries
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();

    }
}
