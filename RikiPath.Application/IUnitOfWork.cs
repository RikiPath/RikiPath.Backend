using RikiPath.Application.IRepositories;

namespace RikiPath.Application
{
    public interface IUnitOfWork
    {
        // 1. Users & Core Auth
        IUserAccountRepository UserAccounts { get; }
        IEmailVerificationRepository EmailVerifications { get; }
        INotificationRepository Notifications { get; }

        // 2. Master Data / CertificateTypes & LanguageSkills
        ICertificateTypeRepository CertificateTypes { get; }
        ICertificateLevelRepository CertificateLevels { get; }
        ILanguageSkillRepository LanguageSkills { get; }
        ICertificationLevelSkillRepository CertificationLevelSkills { get; }

        // 3. Lessons & Learning Progress
        ILessonRepository Lessons { get; }
        ILessonProgressRepository LessonProgresses { get; }

        // 4. Content Banks
        IKanjiRepository Kanjis { get; }
        IKanaCharacterRepository KanaCharacters { get; }
        IKanaWritingPracticeCardRepository KanaWritingPracticeCards { get; }
        IKanaWritingPracticeHistoryRepository KanaWritingPracticeHistories { get; }
        IVocabularyRepository Vocabularies { get; }
        IGrammarPatternRepository GrammarPatterns { get; }
        ILessonKanjiRepository LessonKanjis { get; }
        ILessonVocabularyRepository LessonVocabularies { get; }
        ILessonGrammarRepository LessonGrammars { get; }

        // 5. Personal Vocabulary Notebook
        ILearnerNoteRepository LearnerNotes { get; }
        ILearnerNoteEntryRepository LearnerNoteEntries { get; }

        // 6. JLPT Practice Tests & Submissions
        IMockTestRepository MockTests { get; }
        IMockTestSectionRepository MockTestSections { get; }
        IMockQuestionRepository MockQuestions { get; }
        IMockQuestionOptionRepository MockQuestionOptions { get; }
        IMockTestAttemptRepository MockTestAttempts { get; }
        IMockTestAnswerRepository MockTestAnswers { get; }
        IMockTestSectionResultRepository MockTestSectionResults { get; }
        IPracticeExerciseRepository PracticeExercises { get; }
        IPracticeExerciseAttemptRepository PracticeExerciseAttempts { get; }
        IPracticeQuestionRepository PracticeQuestions { get; }
        IPracticeQuestionOptionRepository PracticeQuestionOptions { get; }
        ILearnerPracticeAnswerRepository LearnerPracticeAnswers { get; }
        IHomeworkAssignmentRepository HomeworkAssignments { get; }
        IHomeworkSubmissionRepository HomeworkSubmissions { get; }
        IPracticeSubmissionRepository PracticeSubmissions { get; }
        IGradingResultRepository GradingResults { get; }

        // 7. Spaced-Repetition Review Queue (SM-2)
        IReviewCardRepository ReviewCards { get; }
        IReviewHistoryRepository ReviewHistories { get; }

        // 8. Subscriptions, Monetization & AI Advisory
        ISubscriptionPlanRepository SubscriptionPlans { get; }
        IFeatureRepository Features { get; }
        IUserSubscriptionRepository UserSubscriptions { get; }
        IAiCreditTopUpRepository AiCreditTopUps { get; }
        IRecommendedLearningPathRepository RecommendedLearningPaths { get; }

        // 9. Paid Mentor 1-1 Service
        IMentorBookingRepository MentorBookings { get; }
        INoteRepository Notes { get; }
        IMentorAvailabilityRepository MentorAvailabilities { get; }

        // Db Helpers
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<T> ExecuteScalarAsync<T>(string sql);
        Task ExecuteRawSqlAsync(string sql);
    }
}
