using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class PracticeTestRepository : GenericRepository<PracticeTest>, IPracticeTestRepository
    {
        public PracticeTestRepository(AppDbContext context) : base(context)
        {
        }

        private IQueryable<PracticeTest> BuildSearchQuery(int? jlptLevelId, ContentStatus? status, string? keyword)
        {
            IQueryable<PracticeTest> query = _context.PracticeTests.AsQueryable();
            if (jlptLevelId.HasValue) query = query.Where(x => x.JlptLevelId == jlptLevelId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrEmpty(keyword)) query = query.Where(x => x.Title.ToLower().Contains(keyword.ToLower()));
            return query;
        }

        public async Task<List<PracticeTest>> SearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword, int pageIndex, int pageSize)
            => await BuildSearchQuery(jlptLevelId, status, keyword)
                .Include(x => x.JlptLevel)
                .OrderByDescending(x => x.CreatedDate)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountSearchAsync(int? jlptLevelId, ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, status, keyword).CountAsync();

        public async Task<List<PracticeTest>> GetPendingReviewAsync()
            => await _context.PracticeTests
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();

        public async Task<PracticeTest?> GetWithSectionsAndQuestionsAsync(int practiceTestId)
            => await _context.PracticeTests
                .Include(x => x.Sections).ThenInclude(s => s.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(x => x.Id == practiceTestId);

        public async Task<List<PracticeTest>> GetByLevelAsync(int jlptLevelId)
        {
            return await _context.PracticeTests
                .Include(t => t.JlptLevel)
                .Where(t => t.JlptLevelId == jlptLevelId && t.Status == ContentStatus.Published)
                .ToListAsync();
        }
    }
}
