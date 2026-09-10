using RikiPath.Domain.Entities;
using RikiPath.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Users
        public DbSet<UserAccount> Users { get; set; }

        // Catalog / master data
        public DbSet<JlptLevel> JlptLevels { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<CourseCategory> CourseCategories { get; set; }

        // Content
        public DbSet<Course> Courses { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonProgress> LessonProgresses { get; set; }

        // Banks
        public DbSet<KanjiEntry> KanjiEntries { get; set; }
        public DbSet<VocabularyEntry> VocabularyEntries { get; set; }
        public DbSet<GrammarPoint> GrammarPoints { get; set; }
        public DbSet<LessonKanji> LessonKanjis { get; set; }
        public DbSet<LessonVocabulary> LessonVocabularies { get; set; }
        public DbSet<LessonGrammar> LessonGrammars { get; set; }

        // Personal vocabulary notebook
        public DbSet<VocabularyList> VocabularyLists { get; set; }
        public DbSet<VocabularyNoteEntry> VocabularyNoteEntries { get; set; }

        // Mock JLPT practice tests
        public DbSet<PracticeTest> PracticeTests { get; set; }
        public DbSet<PracticeTestSection> PracticeTestSections { get; set; }
        public DbSet<PracticeQuestion> PracticeQuestions { get; set; }
        public DbSet<PracticeQuestionOption> PracticeQuestionOptions { get; set; }
        public DbSet<PracticeTestAttempt> PracticeTestAttempts { get; set; }
        public DbSet<PracticeTestAnswer> PracticeTestAnswers { get; set; }
        public DbSet<PracticeTestSectionResult> PracticeTestSectionResults { get; set; }

        // AI learning path & AI grading
        public DbSet<LearningPathSuggestion> LearningPathSuggestions { get; set; }
        public DbSet<PracticeSubmission> PracticeSubmissions { get; set; }
        public DbSet<GradingResult> GradingResults { get; set; }

        // Consultation package
        public DbSet<ConsultationPackage> ConsultationPackages { get; set; }
        public DbSet<ConsultationPurchase> ConsultationPurchases { get; set; }
        public DbSet<CoursePurchase> CoursePurchases { get; set; }
        public DbSet<ConsultationRequest> ConsultationRequests { get; set; }
        public DbSet<ConsultationAnswer> ConsultationAnswers { get; set; }
        public DbSet<ConsultantAvailability> ConsultantAvailabilities { get; set; }

        // Spaced-repetition review queue
        public DbSet<ReviewItem> ReviewItems { get; set; }
        public DbSet<ReviewLog> ReviewLogs { get; set; }

        // Notifications
        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfig());
            modelBuilder.ApplyConfiguration(new JlptLevelConfig());
            modelBuilder.ApplyConfiguration(new SkillConfig());
            modelBuilder.ApplyConfiguration(new CourseCategoryConfig());

            modelBuilder.ApplyConfiguration(new CourseConfig());
            modelBuilder.ApplyConfiguration(new LessonConfig());
            modelBuilder.ApplyConfiguration(new LessonProgressConfig());

            modelBuilder.ApplyConfiguration(new KanjiEntryConfig());
            modelBuilder.ApplyConfiguration(new VocabularyEntryConfig());
            modelBuilder.ApplyConfiguration(new GrammarPointConfig());
            modelBuilder.ApplyConfiguration(new LessonKanjiConfig());
            modelBuilder.ApplyConfiguration(new LessonVocabularyConfig());
            modelBuilder.ApplyConfiguration(new LessonGrammarConfig());

            modelBuilder.ApplyConfiguration(new VocabularyListConfig());
            modelBuilder.ApplyConfiguration(new VocabularyNoteEntryConfig());

            modelBuilder.ApplyConfiguration(new PracticeTestConfig());
            modelBuilder.ApplyConfiguration(new PracticeTestSectionConfig());
            modelBuilder.ApplyConfiguration(new PracticeQuestionConfig());
            modelBuilder.ApplyConfiguration(new PracticeQuestionOptionConfig());
            modelBuilder.ApplyConfiguration(new PracticeTestAttemptConfig());
            modelBuilder.ApplyConfiguration(new PracticeTestAnswerConfig());
            modelBuilder.ApplyConfiguration(new PracticeTestSectionResultConfig());

            modelBuilder.ApplyConfiguration(new LearningPathSuggestionConfig());
            modelBuilder.ApplyConfiguration(new PracticeSubmissionConfig());
            modelBuilder.ApplyConfiguration(new GradingResultConfig());

            modelBuilder.ApplyConfiguration(new ConsultationPackageConfig());
            modelBuilder.ApplyConfiguration(new ConsultationPurchaseConfig());
            modelBuilder.ApplyConfiguration(new ConsultationRequestConfig());
            modelBuilder.ApplyConfiguration(new ConsultationAnswerConfig());
            modelBuilder.ApplyConfiguration(new ConsultantAvailabilityConfig());

            modelBuilder.ApplyConfiguration(new ReviewItemConfig());
            modelBuilder.ApplyConfiguration(new ReviewLogConfig());

            modelBuilder.ApplyConfiguration(new NotificationConfig());
        }
    }
}
