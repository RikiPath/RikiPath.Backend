using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILessonGrammarRepository : IGenericRepository<LessonGrammar>
    {
        Task<List<LessonGrammar>> GetByLessonIdAsync(int lessonId);
        Task<bool> ExistsAsync(int lessonId, int grammarPointId);
    }
}
