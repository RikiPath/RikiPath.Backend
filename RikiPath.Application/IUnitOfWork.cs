using System.Threading.Tasks;
using RikiPath.Application.IRepositories;

namespace RikiPath.Application
{
    public interface IUnitOfWork
    {
        // Users
        IUserAccountRepository UserAccounts { get; }

        // Catalog / master data
        IJlptLevelRepository JlptLevels { get; }
        ISkillRepository Skills { get; }
        ICourseCategoryRepository CourseCategories { get; }

        // Content
        ICourseRepository Courses { get; }
        ICoursePurchaseRepository CoursePurchases { get; }
        ILessonRepository Lessons { get; }
        ILessonProgressRepository LessonProgresses { get; }

        // Banks
        IKanjiEntryRepository KanjiEntries { get; }
        IVocabularyEntryRepository VocabularyEntries { get; }
        IGrammarPointRepository GrammarPoints { get; }
        ILessonKanjiRepository LessonKanjis { get; }
        ILessonVocabularyRepository LessonVocabularies { get; }
        ILessonGrammarRepository LessonGrammars { get; }

        // Personal vocabulary notebook
        IVocabularyListRepository VocabularyLists { get; }
        IVocabularyNoteEntryRepository VocabularyNoteEntries { get; }

        // Mock JLPT practice tests
        IPracticeTestRepository PracticeTests { get; }
        IPracticeTestSectionRepository PracticeTestSections { get; }
        IPracticeQuestionRepository PracticeQuestions { get; }
        IPracticeQuestionOptionRepository PracticeQuestionOptions { get; }
        IPracticeTestAttemptRepository PracticeTestAttempts { get; }
        IPracticeTestAnswerRepository PracticeTestAnswers { get; }
        IPracticeTestSectionResultRepository PracticeTestSectionResults { get; }

        // AI learning path & AI grading
        ILearningPathSuggestionRepository LearningPathSuggestions { get; }
        IPracticeSubmissionRepository PracticeSubmissions { get; }
        IGradingResultRepository GradingResults { get; }

        // Consultation package
        IConsultationPackageRepository ConsultationPackages { get; }
        IConsultationPurchaseRepository ConsultationPurchases { get; }
        IConsultationRequestRepository ConsultationRequests { get; }
        IConsultationAnswerRepository ConsultationAnswers { get; }
        IConsultantAvailabilityRepository ConsultantAvailabilities { get; }

        // Spaced-repetition review queue
        IReviewItemRepository ReviewItems { get; }
        IReviewLogRepository ReviewLogs { get; }

        IEmailVerificationRepository EmailVerifications { get; }
        INotificationRepository Notifications { get; }

        Task SaveChangesAsync();
        Task<T> ExecuteScalarAsync<T>(string sql);
        Task ExecuteRawSqlAsync(string sql);
    }
}
