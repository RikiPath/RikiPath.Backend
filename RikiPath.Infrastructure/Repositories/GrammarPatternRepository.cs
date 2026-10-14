using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class GrammarPatternRepository(AppDbContext context) : GenericRepository<GrammarPattern>(context), IGrammarPatternRepository
    {
        private IQueryable<GrammarPattern> BuildSearchQuery(int? jlptLevelId, string? keyword)
        {
            IQueryable<GrammarPattern> query = _context.GrammarPatterns.AsQueryable();

            // Lọc theo Cấp độ chứng chỉ thông qua bảng trung gian ContentLevelMapping
            if (jlptLevelId.HasValue)
            {
                query = query.Where(x => x.ContentLevelMappings != null &&
                    x.ContentLevelMappings.Any(m => m.CertificateLevelId == jlptLevelId.Value && !m.IsDeleted));
            }

            // Lọc theo từ khóa (Tiêu đề, Cấu trúc, Ghi chú sử dụng, Câu ví dụ)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim();
                query = query.Where(g =>
                    g.Title.Contains(term) ||
                    g.Structure.Contains(term) ||
                    (g.UsageNotes != null && g.UsageNotes.Contains(term)) ||
                    (g.ExampleSentence != null && g.ExampleSentence.Contains(term)));
            }

            return query;
        }

        public async Task<(List<GrammarPattern> Items, int TotalCount)> SearchAsync(
            int? jlptLevelId,
            string? keyword,
            int pageIndex,
            int pageSize)
        {
            var query = BuildSearchQuery(jlptLevelId, keyword);

            var totalCount = await query.CountAsync();

            var items = await query
                .Include(g => g.ContentLevelMappings!)
                    .ThenInclude(m => m.CertificateLevel)
                .OrderByDescending(g => g.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, string? keyword)
            => await BuildSearchQuery(jlptLevelId, keyword).CountAsync();
    }
}