using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class UserAccount : Base
    {
        public int Id { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FirebaseUid { get; set; }
        public bool IsPhoneVerified { get; set; } = false;
        public bool IsEmailVerified { get; set; } = false;
        public Role Role { get; set; }
        public string? AvatarUrl { get; set; }

        public bool SystemNotificationsEnabled { get; set; } = true;
        public bool EmailNotificationsEnabled { get; set; } = true;
        public int DailyStudyMinutes { get; set; } = 30;

        /// <summary>Which certification level the learner is currently targeting — e.g. "JLPT N4".</summary>
        public int? TargetCertificateLevelId { get; set; }
        public CertificateLevel? TargetCertificateLevel { get; set; }
        public string? StudyTimePreference { get; set; }

        // Learning history / streaks
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastStudyDate { get; set; }

        // ---- Navigation properties ----

        // As a Content Author
        public List<Lesson>? AuthoredLessons { get; set; }
        public List<Kanji>? AuthoredKanjis { get; set; }
        public List<Vocabulary>? AuthoredVocabularies { get; set; }
        public List<GrammarPattern>? AuthoredGrammarPatterns { get; set; }
        public List<MockTest>? AuthoredMockTests { get; set; }

        // As Admin reviewer: KHÔNG dùng FK nữa — Lesson/Kanji/Vocabulary/GrammarPattern/
        // MockTest lưu thẳng ReviewedByName (string) vì hệ thống chỉ có 1 Admin duy nhất.

        // As a Learner
        public List<LessonProgress>? LessonProgresses { get; set; }
        public List<LearnerNote>? LearnerNotes { get; set; }
        public List<MockTestAttempt>? MockTestAttempts { get; set; }
        public List<RecommendedLearningPath>? RecommendedLearningPaths { get; set; }
        public List<PracticeSubmission>? PracticeSubmissions { get; set; }
        public List<ReviewCard>? ReviewCards { get; set; }
        public List<LearnerEssay>? LearnerEssays { get; set; }
        public List<UserSubscription>? UserSubscriptions { get; set; }
        public List<AiCreditTopUp>? AiCreditTopUps { get; set; }

        // As a Mentor
        public List<Note>? Notes { get; set; }
        public List<MentorAvailability>? MentorAvailabilities { get; set; }

        // Shared
        public List<EmailVerification>? EmailVerifications { get; set; }
        public List<Notification>? Notifications { get; set; }
    }
}
