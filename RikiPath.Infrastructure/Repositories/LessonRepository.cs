using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class LessonRepository : GenericRepository<Lesson>, ILessonRepository
    {
        public LessonRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Lesson?> GetWithBankLinksAsync(int lessonId)
            => await _context.Lessons
                .Include(x => x.LessonKanjis).ThenInclude(x => x.Kanji)
                .Include(x => x.LessonVocabularies).ThenInclude(x => x.Vocabulary)
                .Include(x => x.LessonGrammars).ThenInclude(x => x.GrammarPattern)
                .FirstOrDefaultAsync(x => x.Id == lessonId);

        public async Task<IReadOnlyList<Lesson>> GetLearnerVisibleByLevelAsync(
            int certificateLevelId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            // Chỉ trả nội dung học đang khả dụng cho cấp độ JLPT của Learner.
            return await LearnerVisibleLessons()
                .Where(lesson => lesson.CertificateLevelId == certificateLevelId)
                .Include(lesson => lesson.LanguageSkill)
                .Include(lesson => lesson.LessonProgresses!
                    .Where(progress => progress.UserId == userId && !progress.IsDeleted))
                // SortOrder là thứ tự nghiệp vụ hiện có; Id là tie-breaker ổn định.
                .OrderBy(lesson => lesson.SortOrder)
                .ThenBy(lesson => lesson.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AssessmentRoadmapLesson>> GetAssessmentRoadmapLessonsAsync(
            int certificateLevelId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await LearnerVisibleLessons()
                .Where(lesson => lesson.CertificateLevelId == certificateLevelId)
                .OrderBy(lesson => lesson.SortOrder)
                .ThenBy(lesson => lesson.Id)
                .Select(lesson => new AssessmentRoadmapLesson(
                    lesson.Id,
                    lesson.Title,
                    lesson.LanguageSkillId,
                    lesson.LanguageSkill.Name,
                    lesson.LessonProgresses!
                        .Where(progress => progress.UserId == userId && !progress.IsDeleted)
                        .OrderByDescending(progress => progress.IsCompleted)
                        .ThenByDescending(progress => progress.ProgressPercent)
                        .FirstOrDefault(),
                    lesson.LessonKanjis!.Count(link => !link.Kanji.IsDeleted),
                    lesson.LessonVocabularies!.Count(link => !link.Vocabulary.IsDeleted),
                    lesson.LessonGrammars!.Count(link => !link.GrammarPattern.IsDeleted)))
                .ToListAsync(cancellationToken);
        }

        public async Task<Lesson?> GetLearnerVisibleByIdAsync(
            int lessonId,
            CancellationToken cancellationToken = default)
        {
            return await LearnerVisibleLessons()
                .Include(lesson => lesson.CertificateLevel)
                    .ThenInclude(level => level.CertificateType)
                .Include(lesson => lesson.LanguageSkill)
                .Include(lesson => lesson.LessonVocabularies!
                    .Where(link => !link.Vocabulary.IsDeleted))
                    .ThenInclude(link => link.Vocabulary)
                .Include(lesson => lesson.LessonKanjis!
                    .Where(link => !link.Kanji.IsDeleted))
                    .ThenInclude(link => link.Kanji)
                .Include(lesson => lesson.LessonGrammars!
                    .Where(link => !link.GrammarPattern.IsDeleted))
                    .ThenInclude(link => link.GrammarPattern)
                .AsSplitQuery()
                .FirstOrDefaultAsync(lesson => lesson.Id == lessonId, cancellationToken);
        }

        private IQueryable<Lesson> LearnerVisibleLessons() =>
            _context.Lessons.AsNoTracking().Where(lesson =>
                !lesson.IsDeleted
                && !lesson.CertificateLevel.IsDeleted
                && !lesson.CertificateLevel.CertificateType.IsDeleted
                && lesson.CertificateLevel.CertificateType.IsActive
                && lesson.CertificateLevel.CertificateType.Name == "JLPT"
                && !lesson.LanguageSkill.IsDeleted);

        public async Task<(int Total, int Completed)> GetCompletionStatsAsync(int userId)
        {
            var total = await _context.Lessons.CountAsync();
            var completed = await _context.LessonProgresses
                .CountAsync(x => x.UserId == userId && x.IsCompleted);

            return (total, completed);
        }

        public async Task<IReadOnlyList<Lesson>> GetByCertificateLevelAsync(int certificateLevelId, CancellationToken cancellationToken = default)
             => await _context.Lessons
                 .AsNoTracking()
                 .Where(lesson =>
                     lesson.CertificateLevelId == certificateLevelId
                     && !lesson.IsDeleted
                     && !lesson.CertificateLevel.IsDeleted
                     && !lesson.CertificateLevel.CertificateType.IsDeleted
                     && lesson.CertificateLevel.CertificateType.IsActive
                     && lesson.CertificateLevel.CertificateType.Name == "JLPT"
                     && !lesson.LanguageSkill.IsDeleted)
                 .Include(x => x.Topic)
                 .Include(x => x.LessonKanjis!.Where(link => !link.Kanji.IsDeleted))
                     .ThenInclude(link => link.Kanji)
                 .Include(x => x.LessonVocabularies!.Where(link => !link.Vocabulary.IsDeleted))
                     .ThenInclude(link => link.Vocabulary)
                 .Include(x => x.LessonGrammars!.Where(link => !link.GrammarPattern.IsDeleted))
                     .ThenInclude(link => link.GrammarPattern)
                 .AsSplitQuery()
                 .OrderBy(x => x.SortOrder)
                 .ThenBy(x => x.Id)
                 .ToListAsync(cancellationToken);
    }
}
