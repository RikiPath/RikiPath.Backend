using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class UserAccount : Base
    {
        public int Id { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; } = false;
        public Role Role { get; set; }
        public string? AvatarUrl { get; set; }

        public bool SystemNotificationsEnabled { get; set; } = true;
        public bool EmailNotificationsEnabled { get; set; } = true;
        public int DailyStudyMinutes { get; set; } = 30;

        /// <summary>Which certification level the learner is currently targeting — e.g. "JLPT N4".</summary>
        public int? TargetCertificationLevelId { get; set; }
        public CertificationLevel? TargetCertificationLevel { get; set; }
        public string? StudyTimePreference { get; set; }

        // Learning history / streaks
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastStudyDate { get; set; }

        // ---- Navigation properties ----

        // As a Content Author
        public List<Lesson>? AuthoredLessons { get; set; }
        public List<KanjiEntry>? AuthoredKanjiEntries { get; set; }
        public List<VocabularyEntry>? AuthoredVocabularyEntries { get; set; }
        public List<GrammarPoint>? AuthoredGrammarPoints { get; set; }
        public List<PracticeTest>? AuthoredPracticeTests { get; set; }

        // As Admin reviewer: KHÔNG dùng FK nữa — Lesson/KanjiEntry/VocabularyEntry/GrammarPoint/
        // PracticeTest lưu thẳng ReviewedByName (string) vì hệ thống chỉ có 1 Admin duy nhất.

        // As a Learner
        public List<LessonProgress>? LessonProgresses { get; set; }
        public List<VocabularyList>? VocabularyLists { get; set; }
        public List<PracticeTestAttempt>? PracticeTestAttempts { get; set; }
        public List<LearningPathSuggestion>? LearningPathSuggestions { get; set; }
        public List<PracticeSubmission>? PracticeSubmissions { get; set; }
        public List<ConsultationPurchase>? ConsultationPurchases { get; set; }
        public List<ReviewItem>? ReviewItems { get; set; }
        public List<UserSubscription>? UserSubscriptions { get; set; }
        public List<AiCreditTopUp>? AiCreditTopUps { get; set; }

        // As a Consultant
        public List<ConsultationRequest>? ConsultationRequestsAsConsultant { get; set; }
        public List<ConsultationAnswer>? ConsultationAnswers { get; set; }
        public List<ConsultantAvailability>? ConsultantAvailabilities { get; set; }

        // Shared
        public List<EmailVerification>? EmailVerifications { get; set; }
        public List<Notification>? Notifications { get; set; }
    }
}
