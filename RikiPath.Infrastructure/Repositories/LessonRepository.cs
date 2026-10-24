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
            return await _context.Lessons
                .AsNoTracking()
                .Where(lesson =>
                    lesson.CertificateLevelId == certificateLevelId
                    && !lesson.IsDeleted
                    && !lesson.CertificateLevel.IsDeleted
                    && !lesson.CertificateLevel.CertificateType.IsDeleted
                    && lesson.CertificateLevel.CertificateType.IsActive
                    && lesson.CertificateLevel.CertificateType.Name == "JLPT"
                    && !lesson.LanguageSkill.IsDeleted)
                .Include(lesson => lesson.LanguageSkill)
                .Include(lesson => lesson.LessonProgresses!
                    .Where(progress => progress.UserId == userId && !progress.IsDeleted))
                // SortOrder là thứ tự nghiệp vụ hiện có; Id là tie-breaker ổn định.
                .OrderBy(lesson => lesson.SortOrder)
                .ThenBy(lesson => lesson.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<Lesson?> GetLearnerVisibleByIdAsync(
            int lessonId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Lessons
                .AsNoTracking()
                .Include(lesson => lesson.CertificateLevel)
                    .ThenInclude(level => level.CertificateType)
                .Include(lesson => lesson.LanguageSkill)
                .FirstOrDefaultAsync(lesson =>
                    lesson.Id == lessonId
                    && !lesson.IsDeleted
                    && !lesson.CertificateLevel.IsDeleted
                    && !lesson.CertificateLevel.CertificateType.IsDeleted
                    && lesson.CertificateLevel.CertificateType.IsActive
                    && lesson.CertificateLevel.CertificateType.Name == "JLPT"
                    && !lesson.LanguageSkill.IsDeleted,
                    cancellationToken);
        }

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
                 .Where(x => x.CertificateLevelId == certificateLevelId)
                 .OrderBy(x => x.SortOrder)
                 .ThenBy(x => x.Id)
                 .ToListAsync(cancellationToken);
    }
}
