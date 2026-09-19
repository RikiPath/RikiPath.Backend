using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Infrastructure.Repositories;
using System.Data;

namespace RikiPath.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        // 1. Users & Core Auth
        public IUserAccountRepository UserAccounts { get; }
        public IEmailVerificationRepository EmailVerifications { get; }
        public INotificationRepository Notifications { get; }

        // 2. Master Data / Certifications & Skills
        public ICertificationRepository Certifications { get; }
        public ICertificationLevelRepository CertificationLevels { get; }
        public ISkillRepository Skills { get; }
        public ICertificationLevelSkillRepository CertificationLevelSkills { get; }

        // 3. Lessons & Learning Progress
        public ILessonRepository Lessons { get; }
        public ILessonProgressRepository LessonProgresses { get; }

        // 4. Content Banks
        public IKanjiEntryRepository KanjiEntries { get; }
        public IVocabularyEntryRepository VocabularyEntries { get; }
        public IGrammarPointRepository GrammarPoints { get; }
        public ILessonKanjiRepository LessonKanjis { get; }
        public ILessonVocabularyRepository LessonVocabularies { get; }
        public ILessonGrammarRepository LessonGrammars { get; }

        // 5. Personal Vocabulary Notebook
        public IVocabularyListRepository VocabularyLists { get; }
        public IVocabularyNoteEntryRepository VocabularyNoteEntries { get; }

        // 6. JLPT Practice Tests & Submissions
        public IPracticeTestRepository PracticeTests { get; }
        public IPracticeTestSectionRepository PracticeTestSections { get; }
        public IPracticeQuestionRepository PracticeQuestions { get; }
        public IPracticeQuestionOptionRepository PracticeQuestionOptions { get; }
        public IPracticeTestAttemptRepository PracticeTestAttempts { get; }
        public IPracticeTestAnswerRepository PracticeTestAnswers { get; }
        public IPracticeTestSectionResultRepository PracticeTestSectionResults { get; }
        public IPracticeSubmissionRepository PracticeSubmissions { get; }
        public IGradingResultRepository GradingResults { get; }

        // 7. Spaced-Repetition Review Queue (SM-2)
        public IReviewItemRepository ReviewItems { get; }
        public IReviewLogRepository ReviewLogs { get; }

        // 8. Subscriptions, Monetization & AI Advisory
        public ISubscriptionPlanRepository SubscriptionPlans { get; }
        public IUserSubscriptionRepository UserSubscriptions { get; }
        public IAiCreditTopUpRepository AiCreditTopUps { get; }
        public ILearningPathSuggestionRepository LearningPathSuggestions { get; }

        // 9. Paid Consultation 1-1 Service
        public IConsultationPackageRepository ConsultationPackages { get; }
        public IConsultationPurchaseRepository ConsultationPurchases { get; }
        public IConsultationRequestRepository ConsultationRequests { get; }
        public IConsultationAnswerRepository ConsultationAnswers { get; }
        public IConsultantAvailabilityRepository ConsultantAvailabilities { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // Initializing Repositories
            UserAccounts = new UserAccountRepository(_context);
            EmailVerifications = new EmailVerificationRepository(_context);
            Notifications = new NotificationRepository(_context);

            Certifications = new CertificationRepository(_context);
            CertificationLevels = new CertificationLevelRepository(_context);
            Skills = new SkillRepository(_context);
            CertificationLevelSkills = new CertificationLevelSkillRepository(_context);

            Lessons = new LessonRepository(_context);
            LessonProgresses = new LessonProgressRepository(_context);

            KanjiEntries = new KanjiEntryRepository(_context);
            VocabularyEntries = new VocabularyEntryRepository(_context);
            GrammarPoints = new GrammarPointRepository(_context);
            LessonKanjis = new LessonKanjiRepository(_context);
            LessonVocabularies = new LessonVocabularyRepository(_context);
            LessonGrammars = new LessonGrammarRepository(_context);

            VocabularyLists = new VocabularyListRepository(_context);
            VocabularyNoteEntries = new VocabularyNoteEntryRepository(_context);

            PracticeTests = new PracticeTestRepository(_context);
            PracticeTestSections = new PracticeTestSectionRepository(_context);
            PracticeQuestions = new PracticeQuestionRepository(_context);
            PracticeQuestionOptions = new PracticeQuestionOptionRepository(_context);
            PracticeTestAttempts = new PracticeTestAttemptRepository(_context);
            PracticeTestAnswers = new PracticeTestAnswerRepository(_context);
            PracticeTestSectionResults = new PracticeTestSectionResultRepository(_context);
            PracticeSubmissions = new PracticeSubmissionRepository(_context);
            GradingResults = new GradingResultRepository(_context);

            ReviewItems = new ReviewItemRepository(_context);
            ReviewLogs = new ReviewLogRepository(_context);

            SubscriptionPlans = new SubscriptionPlanRepository(_context);
            UserSubscriptions = new UserSubscriptionRepository(_context);
            AiCreditTopUps = new AiCreditTopUpRepository(_context);
            LearningPathSuggestions = new LearningPathSuggestionRepository(_context);

            ConsultationPackages = new ConsultationPackageRepository(_context);
            ConsultationPurchases = new ConsultationPurchaseRepository(_context);
            ConsultationRequests = new ConsultationRequestRepository(_context);
            ConsultationAnswers = new ConsultationAnswerRepository(_context);
            ConsultantAvailabilities = new ConsultantAvailabilityRepository(_context);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<T> ExecuteScalarAsync<T>(string sql)
        {
            var connection = _context.Database.GetDbConnection();
            bool wasClosed = connection.State == ConnectionState.Closed;

            try
            {
                if (wasClosed) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                var result = await command.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value) return default!;
                return (T)Convert.ChangeType(result, typeof(T));
            }
            finally
            {
                if (wasClosed && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public async Task ExecuteRawSqlAsync(string sql)
        {
            var connection = _context.Database.GetDbConnection();
            bool wasClosed = connection.State == ConnectionState.Closed;

            try
            {
                if (wasClosed) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (wasClosed && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}