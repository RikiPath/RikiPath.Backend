using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockTestRepository(AppDbContext context) : GenericRepository<MockTest>(context), IMockTestRepository
    {
        private IQueryable<MockTest> BuildSearchQuery(int? jlptLevelId, ContentStatus? status, string? keyword)
        {
            IQueryable<MockTest> query = _context.MockTests.AsQueryable();
            if (jlptLevelId.HasValue) query = query.Where(x => x.CertificateLevelId == jlptLevelId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrEmpty(keyword)) query = query.Where(x => x.Title.ToLower().Contains(keyword.ToLower()));
            return query;
        }

        public async Task<List<MockTest>> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize)
            => await BuildSearchQuery(jlptLevelId, status, keyword)
                .Include(x => x.CertificateLevel)
                .OrderByDescending(x => x.CreatedDate)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, status, keyword).CountAsync();

        public async Task<List<MockTest>> GetPendingReviewAsync()
            => await _context.MockTests
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();

        public async Task<MockTest?> GetWithSectionsAndQuestionsAsync(int practiceTestId)
            => await _context.MockTests
                .Include(x => x.Sections).ThenInclude(s => s.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(x => x.Id == practiceTestId);

        public async Task<List<MockTest>> GetByLevelAsync(int jlptLevelId)
        {
            return await _context.MockTests
                .Include(t => t.CertificateLevel)
                .Where(t => t.CertificateLevelId == jlptLevelId && t.Status == ContentStatus.Published)
                .ToListAsync();
        }
    }
}
