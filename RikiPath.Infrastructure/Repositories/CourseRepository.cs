using RikiPath.Application.IRepositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class CourseRepository : GenericRepository<Course>, ICourseRepository
    {
        public CourseRepository(AppDbContext context) : base(context)
        {
        }

        private IQueryable<Course> BuildSearchQuery(int? jlptLevelId, int? courseCategoryId,
            ContentStatus? status, string? keyword)
        {
            IQueryable<Course> query = _context.Courses.AsQueryable();

            if (jlptLevelId.HasValue) query = query.Where(x => x.JlptLevelId == jlptLevelId.Value);
            if (courseCategoryId.HasValue) query = query.Where(x => x.CourseCategoryId == courseCategoryId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrEmpty(keyword)) query = query.Where(x => x.Title.ToLower().Contains(keyword.ToLower()));

            return query;
        }

        public async Task<List<Course>> SearchAsync(int? jlptLevelId, int? courseCategoryId,
            ContentStatus? status, string? keyword, int pageIndex, int pageSize)
            => await BuildSearchQuery(jlptLevelId, courseCategoryId, status, keyword)
                .Include(x => x.JlptLevel)
                .Include(x => x.CourseCategory)
                .OrderByDescending(x => x.CreatedDate)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountSearchAsync(int? jlptLevelId, int? courseCategoryId,
            ContentStatus? status, string? keyword)
            => await BuildSearchQuery(jlptLevelId, courseCategoryId, status, keyword).CountAsync();

        public async Task<List<Course>> GetPendingReviewAsync()
            => await _context.Courses
                .Include(x => x.ContentAuthor)
                .Where(x => x.Status == ContentStatus.PendingReview)
                .OrderBy(x => x.ModifiedDate)
                .ToListAsync();

        public async Task<Course?> GetWithLessonsAsync(int courseId)
            => await _context.Courses
                .Include(x => x.Lessons.OrderBy(l => l.SortOrder))
                .FirstOrDefaultAsync(x => x.Id == courseId);
    }
}
