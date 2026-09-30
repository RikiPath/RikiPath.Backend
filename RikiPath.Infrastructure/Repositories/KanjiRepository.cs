using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class KanjiRepository(AppDbContext context) : GenericRepository<Kanji>(context), IKanjiRepository
    {
        private IQueryable<Kanji> BuildSearchQuery(int? jlptLevelId, ContentStatus? status, string? keyword)
        {
            IQueryable<Kanji> query = _context.Kanjis.AsQueryable();
            if (jlptLevelId.HasValue) query = query.Where(x => x.CertificateLevelId == jlptLevelId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.Character.Contains(keyword) ||
                    x.Meaning.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }
            return query;
        }

        public async Task<(List<Kanji> Items, int TotalCount)> SearchAsync(
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
                .Include(x => x.CertificateLevel)
                .OrderBy(x => x.Character)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, status, keyword).CountAsync();

        public async Task<List<Kanji>> GetPendingReviewAsync()
            => await _context.Kanjis
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();
    }
}
