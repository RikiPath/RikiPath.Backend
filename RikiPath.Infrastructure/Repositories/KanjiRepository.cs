using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class KanjiRepository(AppDbContext context) : GenericRepository<Kanji>(context), IKanjiRepository
    {
        public Task<List<Kanji>> GetDictionaryEntriesAsync(CancellationToken cancellationToken = default)
            => _context.Kanjis.AsNoTracking()
                .WherePubliclyVisible()
                .Where(x => x.OnYomi != null || x.KunYomi != null)
                .Select(x => new Kanji
                {
                    Id = x.Id, Character = x.Character, Meaning = x.Meaning,
                    SinoVietnamese = x.SinoVietnamese, OnYomi = x.OnYomi, KunYomi = x.KunYomi,
                })
                .ToListAsync(cancellationToken);

        private IQueryable<Kanji> BuildSearchQuery(int? jlptLevelId, string? keyword, bool publicOnly = false)
        {
            IQueryable<Kanji> query = _context.Kanjis.AsQueryable();
            // Chỉ áp dụng quy tắc hiển thị cho luồng Learner/Public.
            // SearchAsync mặc định vẫn giữ dữ liệu phục vụ Admin/Content Management.
            if (publicOnly)
                query = query.WherePubliclyVisible();

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

        public async Task<(List<Kanji> Items, int TotalCount)> SearchPublicAsync(
            int? jlptLevelId,
            string? keyword,
            int? minStroke,
            int? maxStroke,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = BuildSearchQuery(jlptLevelId, keyword, publicOnly: true);

            if (minStroke.HasValue)
                query = query.Where(x => x.StrokeCount >= minStroke.Value);
            if (maxStroke.HasValue)
                query = query.Where(x => x.StrokeCount <= maxStroke.Value);

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .AsNoTracking()
                .Include(x => x.ContentLevelMappings!.Where(mapping => !mapping.IsDeleted))
                    .ThenInclude(mapping => mapping.CertificateLevel)
                .OrderBy(x => x.Character)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public Task<Kanji?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default)
            => _context.Kanjis
                .WherePubliclyVisible()
                .AsNoTracking()
                .Include(x => x.ContentLevelMappings!.Where(mapping => !mapping.IsDeleted))
                    .ThenInclude(mapping => mapping.CertificateLevel)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task<int> CountSearchAsync(int? jlptLevelId, string? keyword)
            => await BuildSearchQuery(jlptLevelId, keyword).CountAsync();
    }
}
