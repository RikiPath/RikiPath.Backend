using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILessonProgressRepository : IGenericRepository<LessonProgress>
    {
        Task<LessonProgress?> GetByUserAndLessonAsync(
            int userId,
            int lessonId,
            CancellationToken cancellationToken = default);
        Task<List<LessonProgress>> GetByUserAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<List<DateTime>> GetActivityDatesAsync(
            int userId,
            CancellationToken cancellationToken = default);
    }
}
