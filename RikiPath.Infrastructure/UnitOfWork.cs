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

        // 2. Master Data / CertificateTypes & LanguageSkills
        public ICertificateTypeRepository CertificateTypes { get; }
        public ICertificateLevelRepository CertificateLevels { get; }
        public ILanguageSkillRepository LanguageSkills { get; }
        public ICertificationLevelSkillRepository CertificationLevelSkills { get; }

        // 3. Lessons & Learning Progress
        public ILessonRepository Lessons { get; }
        public ILessonProgressRepository LessonProgresses { get; }

        // 4. Content Banks
        public IKanjiRepository Kanjis { get; }
        public IKanaCharacterRepository KanaCharacters { get; }
        public IKanaWritingPracticeCardRepository KanaWritingPracticeCards { get; }
        public IKanaWritingPracticeHistoryRepository KanaWritingPracticeHistories { get; }
        public IVocabularyRepository Vocabularies { get; }
        public IGrammarPatternRepository GrammarPatterns { get; }
        public ILessonKanjiRepository LessonKanjis { get; }
        public ILessonVocabularyRepository LessonVocabularies { get; }
        public ILessonGrammarRepository LessonGrammars { get; }

        // 5. Personal Vocabulary Notebook
        public ILearnerNoteRepository LearnerNotes { get; }
        public ILearnerNoteEntryRepository LearnerNoteEntries { get; }
        public ILearnerEssayRepository LearnerEssays { get; }

        // 6. JLPT Practice Tests & Submissions
        public IMockTestRepository MockTests { get; }
        public IMockTestSectionRepository MockTestSections { get; }
        public IMockQuestionRepository MockQuestions { get; }
        public IMockQuestionOptionRepository MockQuestionOptions { get; }
        public IMockTestAttemptRepository MockTestAttempts { get; }
        public IMockTestAnswerRepository MockTestAnswers { get; }
        public IMockTestSectionResultRepository MockTestSectionResults { get; }
        public IPracticeExerciseRepository PracticeExercises { get; }
        public IPracticeExerciseAttemptRepository PracticeExerciseAttempts { get; }
        public IPracticeQuestionRepository PracticeQuestions { get; }
        public IPracticeQuestionOptionRepository PracticeQuestionOptions { get; }
        public ILearnerPracticeAnswerRepository LearnerPracticeAnswers { get; }
        public IHomeworkAssignmentRepository HomeworkAssignments { get; }
        public IHomeworkSubmissionRepository HomeworkSubmissions { get; }
        public IPracticeSubmissionRepository PracticeSubmissions { get; }
        public IGradingResultRepository GradingResults { get; }

        // 7. Spaced-Repetition Review Queue (SM-2)
        public IReviewCardRepository ReviewCards { get; }
        public IReviewHistoryRepository ReviewHistories { get; }

        // 8. Subscriptions, Monetization & AI Advisory
        public ISubscriptionPlanRepository SubscriptionPlans { get; }
        public IFeatureRepository Features { get; }
        public IUserSubscriptionRepository UserSubscriptions { get; }
        public IAiCreditTopUpRepository AiCreditTopUps { get; }
        public IRecommendedLearningPathRepository RecommendedLearningPaths { get; }

        // 9. Paid Mentor 1-1 Service
        public IMentorBookingRepository MentorBookings { get; }
        public INoteRepository Notes { get; }
        public IMentorAvailabilityRepository MentorAvailabilities { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // Initializing Repositories
            UserAccounts = new UserAccountRepository(_context);
            EmailVerifications = new EmailVerificationRepository(_context);
            Notifications = new NotificationRepository(_context);

            CertificateTypes = new CertificateTypeRepository(_context);
            CertificateLevels = new CertificateLevelRepository(_context);
            LanguageSkills = new LanguageSkillRepository(_context);
            CertificationLevelSkills = new CertificationLevelSkillRepository(_context);

            Lessons = new LessonRepository(_context);
            LessonProgresses = new LessonProgressRepository(_context);

            Kanjis = new KanjiRepository(_context);
            KanaCharacters = new KanaCharacterRepository(_context);
            KanaWritingPracticeCards = new KanaWritingPracticeCardRepository(_context);
            KanaWritingPracticeHistories = new KanaWritingPracticeHistoryRepository(_context);
            Vocabularies = new VocabularyRepository(_context);
            GrammarPatterns = new GrammarPatternRepository(_context);
            LessonKanjis = new LessonKanjiRepository(_context);
            LessonVocabularies = new LessonVocabularyRepository(_context);
            LessonGrammars = new LessonGrammarRepository(_context);

            LearnerNotes = new LearnerNoteRepository(_context);
            LearnerNoteEntries = new LearnerNoteEntryRepository(_context);
            LearnerEssays = new LearnerEssayRepository(_context);

            MockTests = new MockTestRepository(_context);
            MockTestSections = new MockTestSectionRepository(_context);
            MockQuestions = new MockQuestionRepository(_context);
            MockQuestionOptions = new MockQuestionOptionRepository(_context);
            MockTestAttempts = new MockTestAttemptRepository(_context);
            MockTestAnswers = new MockTestAnswerRepository(_context);
            MockTestSectionResults = new MockTestSectionResultRepository(_context);
            PracticeExercises = new PracticeExerciseRepository(_context);
            PracticeExerciseAttempts = new PracticeExerciseAttemptRepository(_context);
            PracticeQuestions = new PracticeQuestionRepository(_context);
            PracticeQuestionOptions = new PracticeQuestionOptionRepository(_context);
            LearnerPracticeAnswers = new LearnerPracticeAnswerRepository(_context);
            HomeworkAssignments = new HomeworkAssignmentRepository(_context);
            HomeworkSubmissions = new HomeworkSubmissionRepository(_context);
            PracticeSubmissions = new PracticeSubmissionRepository(_context);
            GradingResults = new GradingResultRepository(_context);

            ReviewCards = new ReviewCardRepository(_context);
            ReviewHistories = new ReviewHistoryRepository(_context);

            SubscriptionPlans = new SubscriptionPlanRepository(_context);
            Features = new FeatureRepository(_context);
            UserSubscriptions = new UserSubscriptionRepository(_context);
            AiCreditTopUps = new AiCreditTopUpRepository(_context);
            RecommendedLearningPaths = new RecommendedLearningPathRepository(_context);

            MentorBookings = new MentorBookingRepository(_context);
            Notes = new NoteRepository(_context);
            MentorAvailabilities = new MentorAvailabilityRepository(_context);
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
