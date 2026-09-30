using RikiPath.Domain.Entities;
using RikiPath.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Infrastructure
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {

        // Users
        public DbSet<UserAccount> Users { get; set; }

        // Catalog / master data
        public DbSet<CertificateType> CertificateTypes { get; set; }
        public DbSet<CertificateLevel> CertificateLevels { get; set; }
        public DbSet<CertificationLevelSkill> CertificationLevelSkills { get; set; }
        public DbSet<LanguageSkill> LanguageSkills { get; set; }

        // Content
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonProgress> LessonProgresses { get; set; }

        // Banks
        public DbSet<Kanji> Kanjis { get; set; }
        public DbSet<KanaCharacter> KanaCharacters { get; set; }
        public DbSet<KanaWritingPracticeCard> KanaWritingPracticeCards { get; set; }
        public DbSet<KanaWritingPracticeHistory> KanaWritingPracticeHistories { get; set; }
        public DbSet<Vocabulary> Vocabularies { get; set; }
        public DbSet<GrammarPattern> GrammarPatterns { get; set; }
        public DbSet<LessonKanji> LessonKanjis { get; set; }
        public DbSet<LessonVocabulary> LessonVocabularies { get; set; }
        public DbSet<LessonGrammar> LessonGrammars { get; set; }

        // Personal vocabulary notebook
        public DbSet<LearnerNote> LearnerNotes { get; set; }
        public DbSet<LearnerNoteEntry> LearnerNoteEntries { get; set; }

        // Mock JLPT-family practice tests
        public DbSet<MockTest> MockTests { get; set; }
        public DbSet<MockTestSection> MockTestSections { get; set; }
        public DbSet<MockQuestion> MockQuestions { get; set; }
        public DbSet<MockQuestionOption> MockQuestionOptions { get; set; }
        public DbSet<MockTestAttempt> MockTestAttempts { get; set; }
        public DbSet<MockTestAnswer> MockTestAnswers { get; set; }
        public DbSet<MockTestSectionResult> MockTestSectionResults { get; set; }
        public DbSet<PracticeExercise> PracticeExercises { get; set; }
        public DbSet<PracticeExerciseAttempt> PracticeExerciseAttempts { get; set; }
        public DbSet<PracticeQuestion> PracticeQuestions { get; set; }
        public DbSet<PracticeQuestionOption> PracticeQuestionOptions { get; set; }
        public DbSet<LearnerPracticeAnswer> LearnerPracticeAnswers { get; set; }
        public DbSet<HomeworkAssignment> HomeworkAssignments { get; set; }
        public DbSet<HomeworkSubmission> HomeworkSubmissions { get; set; }

        // AI learning path & AI grading
        public DbSet<RecommendedLearningPath> RecommendedLearningPaths { get; set; }
        public DbSet<PracticeSubmission> PracticeSubmissions { get; set; }
        public DbSet<GradingResult> GradingResults { get; set; }

        // Mentor 1-1 service
        public DbSet<MentorBooking> MentorBookings { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<MentorAvailability> MentorAvailabilities { get; set; }

        // Subscription / payments
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<Feature> Features { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<AiCreditTopUp> AiCreditTopUps { get; set; }

        // Spaced-repetition review queue
        public DbSet<ReviewCard> ReviewCards { get; set; }
        public DbSet<ReviewHistory> ReviewHistories { get; set; }

        // Notifications
        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfig());
            modelBuilder.ApplyConfiguration(new CertificateTypeConfig());
            modelBuilder.ApplyConfiguration(new CertificateLevelConfig());
            modelBuilder.ApplyConfiguration(new CertificationLevelSkillConfig());
            modelBuilder.ApplyConfiguration(new LanguageSkillConfig());

            modelBuilder.ApplyConfiguration(new LessonConfig());
            modelBuilder.ApplyConfiguration(new LessonProgressConfig());

            modelBuilder.ApplyConfiguration(new KanjiConfig());
            modelBuilder.ApplyConfiguration(new KanaCharacterConfig());
            modelBuilder.ApplyConfiguration(new KanaWritingPracticeCardConfig());
            modelBuilder.ApplyConfiguration(new KanaWritingPracticeHistoryConfig());
            modelBuilder.ApplyConfiguration(new VocabularyConfig());
            modelBuilder.ApplyConfiguration(new GrammarPatternConfig());
            modelBuilder.ApplyConfiguration(new LessonKanjiConfig());
            modelBuilder.ApplyConfiguration(new LessonVocabularyConfig());
            modelBuilder.ApplyConfiguration(new LessonGrammarConfig());

            modelBuilder.ApplyConfiguration(new LearnerNoteConfig());
            modelBuilder.ApplyConfiguration(new LearnerNoteEntryConfig());

            modelBuilder.ApplyConfiguration(new MockTestConfig());
            modelBuilder.ApplyConfiguration(new MockTestSectionConfig());
            modelBuilder.ApplyConfiguration(new MockQuestionConfig());
            modelBuilder.ApplyConfiguration(new MockQuestionOptionConfig());
            modelBuilder.ApplyConfiguration(new MockTestAttemptConfig());
            modelBuilder.ApplyConfiguration(new MockTestAnswerConfig());
            modelBuilder.ApplyConfiguration(new MockTestSectionResultConfig());
            modelBuilder.ApplyConfiguration(new PracticeExerciseConfig());
            modelBuilder.ApplyConfiguration(new PracticeExerciseAttemptConfig());
            modelBuilder.ApplyConfiguration(new PracticeQuestionConfig());
            modelBuilder.ApplyConfiguration(new PracticeQuestionOptionConfig());
            modelBuilder.ApplyConfiguration(new LearnerPracticeAnswerConfig());
            modelBuilder.ApplyConfiguration(new HomeworkAssignmentConfig());
            modelBuilder.ApplyConfiguration(new HomeworkSubmissionConfig());

            modelBuilder.ApplyConfiguration(new RecommendedLearningPathConfig());
            modelBuilder.ApplyConfiguration(new PracticeSubmissionConfig());
            modelBuilder.ApplyConfiguration(new GradingResultConfig());

            modelBuilder.ApplyConfiguration(new MentorBookingConfig());
            modelBuilder.ApplyConfiguration(new NoteConfig());
            modelBuilder.ApplyConfiguration(new MentorAvailabilityConfig());

            modelBuilder.ApplyConfiguration(new SubscriptionPlanConfig());
            modelBuilder.ApplyConfiguration(new FeatureConfig());
            modelBuilder.ApplyConfiguration(new UserSubscriptionConfig());
            modelBuilder.ApplyConfiguration(new AiCreditTopUpConfig());

            modelBuilder.ApplyConfiguration(new ReviewCardConfig());
            modelBuilder.ApplyConfiguration(new ReviewHistoryConfig());

            modelBuilder.ApplyConfiguration(new NotificationConfig());
        }
    }
}
