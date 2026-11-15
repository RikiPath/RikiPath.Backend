using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface ILessonRepository : IGenericRepository<Lesson>
    {
        Task<Lesson?> GetWithBankLinksAsync(int lessonId);
        Task<IReadOnlyList<Lesson>> GetLearnerVisibleByLevelAsync(
            int certificateLevelId,
            int userId,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AssessmentRoadmapLesson>> GetAssessmentRoadmapLessonsAsync(
            int certificateLevelId,
            int userId,
            CancellationToken cancellationToken = default);
        Task<Lesson?> GetLearnerVisibleByIdAsync(
            int lessonId,
            CancellationToken cancellationToken = default);
        Task<(int Total, int Completed)> GetCompletionStatsAsync(int userId);
        Task<IReadOnlyList<Lesson>> GetByCertificateLevelAsync(int certificateLevelId, CancellationToken cancellationToken = default);
    }

    public sealed record AssessmentRoadmapLesson(
        int Id,
        string Title,
        int LanguageSkillId,
        string LanguageSkillName,
        LessonProgress? Progress,
        int KanjiCount,
        int VocabularyCount,
        int GrammarCount);
}
