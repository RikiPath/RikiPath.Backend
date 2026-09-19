using RikiPath.Application.IRepositories;

namespace RikiPath.Application
{
    public interface IUnitOfWork
    {
        // 1. Users & Core Auth
        IUserAccountRepository UserAccounts { get; }
        IEmailVerificationRepository EmailVerifications { get; }
        INotificationRepository Notifications { get; }

        // 2. Master Data / Certifications & Skills
        ICertificationRepository Certifications { get; }
        ICertificationLevelRepository CertificationLevels { get; }
        ISkillRepository Skills { get; }
        ICertificationLevelSkillRepository CertificationLevelSkills { get; }

        // 3. Lessons & Learning Progress
        ILessonRepository Lessons { get; }
        ILessonProgressRepository LessonProgresses { get; }

        // 4. Content Banks
        IKanjiEntryRepository KanjiEntries { get; }
        IVocabularyEntryRepository VocabularyEntries { get; }
        IGrammarPointRepository GrammarPoints { get; }
        ILessonKanjiRepository LessonKanjis { get; }
        ILessonVocabularyRepository LessonVocabularies { get; }
        ILessonGrammarRepository LessonGrammars { get; }

        // 5. Personal Vocabulary Notebook
        IVocabularyListRepository VocabularyLists { get; }
        IVocabularyNoteEntryRepository VocabularyNoteEntries { get; }

        // 6. JLPT Practice Tests & Submissions
        IPracticeTestRepository PracticeTests { get; }
        IPracticeTestSectionRepository PracticeTestSections { get; }
        IPracticeQuestionRepository PracticeQuestions { get; }
        IPracticeQuestionOptionRepository PracticeQuestionOptions { get; }
        IPracticeTestAttemptRepository PracticeTestAttempts { get; }
        IPracticeTestAnswerRepository PracticeTestAnswers { get; }
        IPracticeTestSectionResultRepository PracticeTestSectionResults { get; }
        IPracticeSubmissionRepository PracticeSubmissions { get; }
        IGradingResultRepository GradingResults { get; }

        // 7. Spaced-Repetition Review Queue (SM-2)
        IReviewItemRepository ReviewItems { get; }
        IReviewLogRepository ReviewLogs { get; }

        // 8. Subscriptions, Monetization & AI Advisory
        ISubscriptionPlanRepository SubscriptionPlans { get; }
        IUserSubscriptionRepository UserSubscriptions { get; }
        IAiCreditTopUpRepository AiCreditTopUps { get; }
        ILearningPathSuggestionRepository LearningPathSuggestions { get; }

        // 9. Paid Consultation 1-1 Service
        IConsultationPackageRepository ConsultationPackages { get; }
        IConsultationPurchaseRepository ConsultationPurchases { get; }
        IConsultationRequestRepository ConsultationRequests { get; }
        IConsultationAnswerRepository ConsultationAnswers { get; }
        IConsultantAvailabilityRepository ConsultantAvailabilities { get; }

        // Db Helpers
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<T> ExecuteScalarAsync<T>(string sql);
        Task ExecuteRawSqlAsync(string sql);
    }
}