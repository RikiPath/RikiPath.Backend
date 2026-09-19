using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class UserConfig : IEntityTypeConfiguration<UserAccount>
    {
        public void Configure(EntityTypeBuilder<UserAccount> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
            builder.HasIndex(x => x.Email).IsUnique();

            builder.Property(x => x.FirstName).HasMaxLength(100);
            builder.Property(x => x.LastName).HasMaxLength(100);
            builder.Property(x => x.PhoneNumber).HasMaxLength(20);
            builder.Property(x => x.AvatarUrl).HasMaxLength(500);
            builder.Property(x => x.StudyTimePreference).HasMaxLength(50);
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

            // Learner's target certification level — optional, does not cascade-delete the user
            builder.HasOne(x => x.TargetCertificationLevel)
                .WithMany(x => x.LearnersTargeting)
                .HasForeignKey(x => x.TargetCertificationLevelId)
                .OnDelete(DeleteBehavior.SetNull);

            // ---- As a Content Author ----
            // Không còn cấu hình Reviewed* nữa: Lesson/KanjiEntry/VocabularyEntry/GrammarPoint/
            // PracticeTest giờ lưu thẳng "ReviewedByName" (string), không phải FK về UserAccount,
            // vì hệ thống chỉ có 1 Admin duy nhất — không cần navigation/relationship cho việc này.

            builder.HasMany(x => x.AuthoredLessons)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredKanjiEntries)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredVocabularyEntries)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredGrammarPoints)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredPracticeTests)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- As a Learner ----

            builder.HasMany(x => x.LessonProgresses)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.VocabularyLists)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.PracticeTestAttempts)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.LearningPathSuggestions)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.PracticeSubmissions)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.ConsultationPurchases)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ReviewItems)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Lịch sử mua gói/nạp lượt AI - giữ Restrict để không mất dữ liệu tài chính
            // nếu sau này có thao tác xóa cứng UserAccount.
            builder.HasMany(x => x.UserSubscriptions)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AiCreditTopUps)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- As a Consultant ----

            builder.HasMany(x => x.ConsultationRequestsAsConsultant)
                .WithOne(x => x.Consultant)
                .HasForeignKey(x => x.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ConsultationAnswers)
                .WithOne(x => x.Consultant)
                .HasForeignKey(x => x.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ConsultantAvailabilities)
                .WithOne(x => x.Consultant)
                .HasForeignKey(x => x.ConsultantId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---- Shared ----

            builder.HasMany(x => x.EmailVerifications)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Notifications)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}