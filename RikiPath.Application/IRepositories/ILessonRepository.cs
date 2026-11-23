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
        int GrammarCount,
        string? Description = null,
        int SortOrder = 0,
        int EstimatedMinutes = 15,
        int? TopicId = null,
        string? TopicTitle = null,
        int? TopicSortOrder = null,
        string? TopicStageCode = null,
        string? TopicObjectives = null,
        int? PrerequisiteLessonId = null,
        string? LearningObjectives = null,
        string? LanguageSkillCode = null);
}
