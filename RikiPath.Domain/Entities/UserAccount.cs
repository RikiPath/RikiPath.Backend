using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>
    /// A single account table for every role in the system (Learner, ContentAuthor,
    /// Consultant, Admin) — discriminated by <see cref="Role"/> instead of per-role tables.
    /// </summary>
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
        public int? TargetJlptLevelId { get; set; }
        public JlptLevel? TargetJlptLevel { get; set; }
        public string? StudyTimePreference { get; set; }

        // Learning history / streaks
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastStudyDate { get; set; }

        // ---- Navigation properties ----

        // As a Content Author
        public List<Course>? AuthoredCourses { get; set; }
        public List<KanjiEntry>? AuthoredKanjiEntries { get; set; }
        public List<VocabularyEntry>? AuthoredVocabularyEntries { get; set; }
        public List<GrammarPoint>? AuthoredGrammarPoints { get; set; }
        public List<PracticeTest>? AuthoredPracticeTests { get; set; }

        // As Admin reviewer
        public List<Course>? ReviewedCourses { get; set; }
        public List<KanjiEntry>? ReviewedKanjiEntries { get; set; }
        public List<VocabularyEntry>? ReviewedVocabularyEntries { get; set; }
        public List<GrammarPoint>? ReviewedGrammarPoints { get; set; }
        public List<PracticeTest>? ReviewedPracticeTests { get; set; }

        // As a Learner
        public List<LessonProgress>? LessonProgresses { get; set; }
        public List<VocabularyList>? VocabularyLists { get; set; }
        public List<PracticeTestAttempt>? PracticeTestAttempts { get; set; }
        public List<LearningPathSuggestion>? LearningPathSuggestions { get; set; }
        public List<PracticeSubmission>? PracticeSubmissions { get; set; }
        public List<ConsultationPurchase>? ConsultationPurchases { get; set; }
        public List<ReviewItem>? ReviewItems { get; set; }

        // As a Consultant
        public List<ConsultationRequest>? ConsultationRequestsAsConsultant { get; set; }
        public List<ConsultationAnswer>? ConsultationAnswers { get; set; }
        public List<ConsultantAvailability>? ConsultantAvailabilities { get; set; }

        // Shared
        public List<EmailVerification>? EmailVerifications { get; set; }
        public List<Notification>? Notifications { get; set; }
    }
}
