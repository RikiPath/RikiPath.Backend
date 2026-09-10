using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILessonKanjiRepository : IGenericRepository<LessonKanji>
    {
        Task<List<LessonKanji>> GetByLessonIdAsync(int lessonId);
        Task<bool> ExistsAsync(int lessonId, int kanjiEntryId);
    }
}
