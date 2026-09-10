using RikiPath.Application.IRepositories;
using RikiPath.Infrastructure.Repositories;
using RikiPath.Application;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IUserAccountRepository UserAccounts { get; }

        public IJlptLevelRepository JlptLevels { get; }
        public ISkillRepository Skills { get; }
        public ICourseCategoryRepository CourseCategories { get; }

        public ICourseRepository Courses { get; }
        public ICoursePurchaseRepository CoursePurchases { get; }
        public ILessonRepository Lessons { get; }
        public ILessonProgressRepository LessonProgresses { get; }

        public IKanjiEntryRepository KanjiEntries { get; }
        public IVocabularyEntryRepository VocabularyEntries { get; }
        public IGrammarPointRepository GrammarPoints { get; }
        public ILessonKanjiRepository LessonKanjis { get; }
        public ILessonVocabularyRepository LessonVocabularies { get; }
        public ILessonGrammarRepository LessonGrammars { get; }

        public IVocabularyListRepository VocabularyLists { get; }
        public IVocabularyNoteEntryRepository VocabularyNoteEntries { get; }

        public IPracticeTestRepository PracticeTests { get; }
        public IPracticeTestSectionRepository PracticeTestSections { get; }
        public IPracticeQuestionRepository PracticeQuestions { get; }
        public IPracticeQuestionOptionRepository PracticeQuestionOptions { get; }
        public IPracticeTestAttemptRepository PracticeTestAttempts { get; }
        public IPracticeTestAnswerRepository PracticeTestAnswers { get; }
        public IPracticeTestSectionResultRepository PracticeTestSectionResults { get; }

        public ILearningPathSuggestionRepository LearningPathSuggestions { get; }
        public IPracticeSubmissionRepository PracticeSubmissions { get; }
        public IGradingResultRepository GradingResults { get; }

        public IConsultationPackageRepository ConsultationPackages { get; }
        public IConsultationPurchaseRepository ConsultationPurchases { get; }
        public IConsultationRequestRepository ConsultationRequests { get; }
        public IConsultationAnswerRepository ConsultationAnswers { get; }
        public IConsultantAvailabilityRepository ConsultantAvailabilities { get; }

        public IReviewItemRepository ReviewItems { get; }
        public IReviewLogRepository ReviewLogs { get; }

        public IEmailVerificationRepository EmailVerifications { get; }
        public INotificationRepository Notifications { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;

            UserAccounts = new UserAccountRepository(context);

            JlptLevels = new JlptLevelRepository(context);
            Skills = new SkillRepository(context);
            CourseCategories = new CourseCategoryRepository(context);

            Courses = new CourseRepository(context);
            CoursePurchases = new CoursePurchaseRepository(context);
            Lessons = new LessonRepository(context);
            LessonProgresses = new LessonProgressRepository(context);

            KanjiEntries = new KanjiEntryRepository(context);
            VocabularyEntries = new VocabularyEntryRepository(context);
            GrammarPoints = new GrammarPointRepository(context);
            LessonKanjis = new LessonKanjiRepository(context);
            LessonVocabularies = new LessonVocabularyRepository(context);
            LessonGrammars = new LessonGrammarRepository(context);

            VocabularyLists = new VocabularyListRepository(context);
            VocabularyNoteEntries = new VocabularyNoteEntryRepository(context);

            PracticeTests = new PracticeTestRepository(context);
            PracticeTestSections = new PracticeTestSectionRepository(context);
            PracticeQuestions = new PracticeQuestionRepository(context);
            PracticeQuestionOptions = new PracticeQuestionOptionRepository(context);
            PracticeTestAttempts = new PracticeTestAttemptRepository(context);
            PracticeTestAnswers = new PracticeTestAnswerRepository(context);
            PracticeTestSectionResults = new PracticeTestSectionResultRepository(context);

            LearningPathSuggestions = new LearningPathSuggestionRepository(context);
            PracticeSubmissions = new PracticeSubmissionRepository(context);
            GradingResults = new GradingResultRepository(context);

            ConsultationPackages = new ConsultationPackageRepository(context);
            ConsultationPurchases = new ConsultationPurchaseRepository(context);
            ConsultationRequests = new ConsultationRequestRepository(context);
            ConsultationAnswers = new ConsultationAnswerRepository(context);
            ConsultantAvailabilities = new ConsultantAvailabilityRepository(context);

            ReviewItems = new ReviewItemRepository(context);
            ReviewLogs = new ReviewLogRepository(context);

            EmailVerifications = new EmailVerificationRepository(context);
            Notifications = new NotificationRepository(context);
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }

        public async Task<T> ExecuteScalarAsync<T>(string sql)
        {
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                try
                {
                    if (command.Connection!.State != System.Data.ConnectionState.Open)
                    {
                        await command.Connection.OpenAsync();
                    }

                    command.CommandText = sql;
                    var result = await command.ExecuteScalarAsync();
                    return (T)Convert.ChangeType(result, typeof(T));
                }
                finally
                {
                    if (command.Connection!.State == System.Data.ConnectionState.Open)
                    {
                        await command.Connection.CloseAsync();
                    }
                }
            }
        }

        public async Task ExecuteRawSqlAsync(string sql)
        {
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                try
                {
                    if (command.Connection!.State != System.Data.ConnectionState.Open)
                    {
                        await command.Connection.OpenAsync();
                    }

                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    if (command.Connection!.State == System.Data.ConnectionState.Open)
                    {
                        await command.Connection.CloseAsync();
                    }
                }
            }
        }
    }
}