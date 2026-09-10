using RikiPath.Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ICourseRepository : IGenericRepository<Course>
    {
        Task<List<Course>> SearchAsync(int? jlptLevelId, int? courseCategoryId,
            ContentStatus? status, string? keyword, int pageIndex, int pageSize);
        Task<int> CountSearchAsync(int? jlptLevelId, int? courseCategoryId,
            ContentStatus? status, string? keyword);
        Task<List<Course>> GetPendingReviewAsync();
        Task<Course?> GetWithLessonsAsync(int courseId);
    }
}
