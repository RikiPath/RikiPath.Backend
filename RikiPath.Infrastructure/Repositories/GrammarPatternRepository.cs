using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class GrammarPatternRepository(AppDbContext context) : GenericRepository<GrammarPattern>(context), IGrammarPatternRepository
    {
        private IQueryable<GrammarPattern> BuildSearchQuery(int? jlptLevelId, ContentStatus? status, string? keyword)
        {
            IQueryable<GrammarPattern> query = _context.GrammarPatterns.AsQueryable();
            if (jlptLevelId.HasValue) query = query.Where(x => x.CertificateLevelId == jlptLevelId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(x => x.Title.ToLower().Contains(keyword.ToLower()) || x.Structure.ToLower().Contains(keyword.ToLower()));
            return query;
        }

        public async Task<(List<GrammarPattern> Items, int TotalCount)> SearchAsync(
    int? jlptLevelId,
    ContentStatus? status,
    string? keyword,
    int pageIndex,
    int pageSize)
        {
            var query = _context.GrammarPatterns
                .Include(g => g.CertificateLevel)
                .AsQueryable();

            if (jlptLevelId.HasValue)
            {
                query = query.Where(g => g.CertificateLevelId == jlptLevelId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(g => g.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim();
                query = query.Where(g =>
                    g.Title.Contains(term) ||
                    g.Structure.Contains(term) ||
                    (g.UsageNotes != null && g.UsageNotes.Contains(term)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(g => g.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, status, keyword).CountAsync();

        public async Task<List<GrammarPattern>> GetPendingReviewAsync()
            => await _context.GrammarPatterns
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();
    }
}
