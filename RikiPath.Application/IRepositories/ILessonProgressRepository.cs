using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILessonProgressRepository : IGenericRepository<LessonProgress>
    {
        Task<LessonProgress?> GetByUserAndLessonAsync(int userId, int lessonId);
        Task<List<LessonProgress>> GetByUserAsync(int userId);
        Task<List<DateTime>> GetActivityDatesAsync(int userId);
    }
}
