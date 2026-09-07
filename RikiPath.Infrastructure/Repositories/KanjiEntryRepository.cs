using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class KanjiEntryRepository : GenericRepository<KanjiEntry>, IKanjiEntryRepository
    {
        public KanjiEntryRepository(AppDbContext context) : base(context)
        {
        }

        private IQueryable<KanjiEntry> BuildSearchQuery(int? jlptLevelId, ContentStatus? status, string? keyword)
        {
            IQueryable<KanjiEntry> query = _context.KanjiEntries.AsQueryable();
            if (jlptLevelId.HasValue) query = query.Where(x => x.JlptLevelId == jlptLevelId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(x => x.Character.Contains(keyword) || x.Meaning.ToLower().Contains(keyword.ToLower()));
            return query;
        }

        public async Task<(List<KanjiEntry> Items, int TotalCount)> SearchAsync(
            int? jlptLevelId,
            ContentStatus? status,
            string? keyword,
            int? minStroke,
            int? maxStroke,
            int pageIndex,
            int pageSize)
        {
            var query = BuildSearchQuery(jlptLevelId, status, keyword);

            if (minStroke.HasValue)
            {
                query = query.Where(x => x.StrokeCount >= minStroke.Value);
            }

            if (maxStroke.HasValue)
            {
                query = query.Where(x => x.StrokeCount <= maxStroke.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .Include(x => x.JlptLevel)
                .OrderBy(x => x.Character)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, status, keyword).CountAsync();

        public async Task<List<KanjiEntry>> GetPendingReviewAsync()
            => await _context.KanjiEntries
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();
    }
}
