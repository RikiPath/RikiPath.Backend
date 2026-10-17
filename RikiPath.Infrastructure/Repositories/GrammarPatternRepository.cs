using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class GrammarPatternRepository(AppDbContext context) : GenericRepository<GrammarPattern>(context), IGrammarPatternRepository
    {
        private IQueryable<GrammarPattern> BuildSearchQuery(int? jlptLevelId, string? keyword, bool publicOnly = false)
        {
            IQueryable<GrammarPattern> query = _context.GrammarPatterns.AsQueryable();
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

        public async Task<(List<GrammarPattern> Items, int TotalCount)> SearchPublicAsync(
            int? jlptLevelId,
            string? keyword,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = BuildSearchQuery(jlptLevelId, keyword, publicOnly: true);
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .AsNoTracking()
                .Include(x => x.ContentLevelMappings!.Where(mapping => !mapping.IsDeleted))
                    .ThenInclude(mapping => mapping.CertificateLevel)
                .OrderByDescending(x => x.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public Task<GrammarPattern?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default)
            => _context.GrammarPatterns
                .WherePubliclyVisible()
                .AsNoTracking()
                .Include(x => x.ContentLevelMappings!.Where(mapping => !mapping.IsDeleted))
                    .ThenInclude(mapping => mapping.CertificateLevel)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task<int> CountSearchAsync(int? jlptLevelId, string? keyword)
            => await BuildSearchQuery(jlptLevelId, keyword).CountAsync();
    }
}
