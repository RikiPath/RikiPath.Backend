using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class KanjiRepository(AppDbContext context) : GenericRepository<Kanji>(context), IKanjiRepository
    {
        private IQueryable<Kanji> BuildSearchQuery(int? jlptLevelId, string? keyword)
        {
            IQueryable<Kanji> query = _context.Kanjis.AsQueryable();

            // Lọc theo Cấp độ chứng chỉ thông qua bảng trung gian ContentLevelMapping
            if (jlptLevelId.HasValue)
            {
                query = query.Where(x => x.ContentLevelMappings != null &&
                    x.ContentLevelMappings.Any(m => m.CertificateLevelId == jlptLevelId.Value && !m.IsDeleted));
            }

            // Lọc theo từ khóa (Chữ Hán, Ý nghĩa, Hán Việt)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(x =>
                    x.Character.Contains(kw) ||
                    x.Meaning.Contains(kw) ||
                    (x.SinoVietnamese != null && x.SinoVietnamese.Contains(kw)));
            }

            return query;
        }

        public async Task<(List<Kanji> Items, int TotalCount)> SearchAsync(
            int? jlptLevelId,
            string? keyword,
            int? minStroke,
            int? maxStroke,
            int pageIndex,
            int pageSize)
        {
            var query = BuildSearchQuery(jlptLevelId, keyword);

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
                .Include(x => x.ContentLevelMappings!)
                    .ThenInclude(m => m.CertificateLevel)
                .OrderBy(x => x.Character)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, string? keyword)
            => await BuildSearchQuery(jlptLevelId, keyword).CountAsync();
    }
}