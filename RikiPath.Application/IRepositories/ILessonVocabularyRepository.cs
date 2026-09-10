using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILessonVocabularyRepository : IGenericRepository<LessonVocabulary>
    {
        Task<List<LessonVocabulary>> GetByLessonIdAsync(int lessonId);
        Task<bool> ExistsAsync(int lessonId, int vocabularyEntryId);
    }
}
