using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class VocabularyRepository(AppDbContext context) : GenericRepository<Vocabulary>(context), IVocabularyRepository
    {
        private IQueryable<Vocabulary> BuildSearchQuery(int? jlptLevelId, ContentStatus? status, string? keyword)
        {
            IQueryable<Vocabulary> query = _context.Vocabularies.AsQueryable();
            if (jlptLevelId.HasValue) query = query.Where(x => x.CertificateLevelId == jlptLevelId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(x => x.Word.Contains(keyword) || x.Reading.Contains(keyword) || x.Meaning.ToLower().Contains(keyword.ToLower()));
            return query;
        }

        public async Task<(List<Vocabulary> items, int total)> SearchAsync(
             int? jlptLevelId,
             ContentStatus? status,
             string? keyword,
             int pageIndex,
             int pageSize)
        {
            var query = BuildSearchQuery(jlptLevelId, status, keyword);

            var total = await query.CountAsync();

            var items = await query
                .Include(x => x.CertificateLevel)
                .OrderBy(x => x.Word)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, status, keyword).CountAsync();

        public async Task<List<Vocabulary>> GetPendingReviewAsync()
            => await _context.Vocabularies
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();

    }
}
