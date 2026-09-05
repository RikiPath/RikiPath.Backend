using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILessonRepository : IGenericRepository<Lesson>
    {
        Task<List<Lesson>> GetByCourseAsync(int courseId);
        Task<Lesson?> GetWithBankLinksAsync(int lessonId);
        Task<(int Total, int Completed)> GetCompletionStatsAsync(int userId);
    }
}
