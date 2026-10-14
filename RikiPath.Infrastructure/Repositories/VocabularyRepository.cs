using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class VocabularyRepository(AppDbContext context) : GenericRepository<Vocabulary>(context), IVocabularyRepository
    {
        private IQueryable<Vocabulary> BuildSearchQuery(int? jlptLevelId, string? keyword)
        {
            IQueryable<Vocabulary> query = _context.Vocabularies.AsQueryable();

            // Lọc theo Cấp độ chứng chỉ thông qua bảng trung gian ContentLevelMapping
            if (jlptLevelId.HasValue)
            {
                query = query.Where(x => x.ContentLevelMappings != null &&
                    x.ContentLevelMappings.Any(m => m.CertificateLevelId == jlptLevelId.Value && !m.IsDeleted));
            }

            // Lọc theo Từ khóa
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(x =>
                    x.Word.Contains(kw) ||
                    x.Reading.Contains(kw) ||
                    x.Meaning.Contains(kw));
            }

            return query;
        }

        public async Task<(List<Vocabulary> items, int total)> SearchAsync(
            int? jlptLevelId,
            string? keyword,
            int pageIndex,
            int pageSize)
        {
            var query = BuildSearchQuery(jlptLevelId, keyword);

            var total = await query.CountAsync();

            var items = await query
                .Include(x => x.ContentLevelMappings!)
                    .ThenInclude(m => m.CertificateLevel)
                .OrderBy(x => x.Word)
                .Skip((pageIndex - 1) * pageSize) // Đã sửa lỗi phân trang
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, string? keyword)
            => await BuildSearchQuery(jlptLevelId, keyword).CountAsync();
    }
}