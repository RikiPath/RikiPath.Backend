using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace RikiPath.Infrastructure.Configuration
{
    public class UserConfig : IEntityTypeConfiguration<UserAccount>
    {
        public void Configure(EntityTypeBuilder<UserAccount> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.TargetCertificateLevelId).HasColumnName("TargetCertificationLevelId");

            builder.HasIndex(x => x.Email).IsUnique();

            builder.Property(x => x.FirstName).HasMaxLength(100);
            builder.Property(x => x.LastName).HasMaxLength(100);
            builder.Property(x => x.PhoneNumber).HasMaxLength(20);
            builder.Property(x => x.AvatarUrl).HasMaxLength(500);
            builder.Property(x => x.StudyTimePreference).HasMaxLength(50);
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

            // Deterministic development accounts. Fixed salts keep EF HasData stable
            // between design-time model builds and can be verified by AuthService.
            builder.HasData(
                CreateSeedUser(900001, "admin@rikipath.local", "Admin", "System", Role.Admin, "Admin@123"),
                CreateSeedUser(900002, "mentor1@rikipath.local", "Mentor", "One", Role.Mentor, "Mentor1@123"),
                CreateSeedUser(900003, "mentor2@rikipath.local", "Mentor", "Two", Role.Mentor, "Mentor2@123"),
                CreateSeedUser(900004, "author1@rikipath.local", "Content", "Author One", Role.ContentAuthor, "Author1@123"),
                CreateSeedUser(900005, "author2@rikipath.local", "Content", "Author Two", Role.ContentAuthor, "Author2@123"),
                CreateSeedUser(900007, "learner.meeting@rikipath.local", "Meeting", "Test Learner", Role.Learner, "Learner1@123"),
                CreateSeedUser(900011, "learner2.meeting@rikipath.local", "Linh", "Nguyen", Role.Learner, "Learner2@123"),
                CreateSeedUser(900009, "learner3.meeting@rikipath.local", "Minh", "Tran", Role.Learner, "Learner3@123"),
                CreateSeedUser(900010, "learner4.meeting@rikipath.local", "Hoang", "Pham", Role.Learner, "Learner4@123")
            );

            // Learner's target certification level — optional, does not cascade-delete the user
            builder.HasOne(x => x.TargetCertificateLevel)
                .WithMany(x => x.LearnersTargeting)
                .HasForeignKey(x => x.TargetCertificateLevelId)
                .OnDelete(DeleteBehavior.SetNull);

            // ---- As a Content Author ----
            // Không còn cấu hình Reviewed* nữa: Lesson/Kanji/Vocabulary/GrammarPattern/
            // MockTest giờ lưu thẳng "ReviewedByName" (string), không phải FK về UserAccount,
            // vì hệ thống chỉ có 1 Admin duy nhất — không cần navigation/relationship cho việc này.

            builder.HasMany(x => x.AuthoredLessons)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredKanjis)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredVocabularies)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredGrammarPatterns)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredMockTests)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- As a Learner ----

            builder.HasMany(x => x.LessonProgresses)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.LearnerNotes)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.MockTestAttempts)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.RecommendedLearningPaths)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.PracticeSubmissions)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.ReviewCards)
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

            // ---- As a Mentor ----

            builder.HasMany(x => x.Notes)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.MentorAvailabilities)
                .WithOne(x => x.Mentor)
                .HasForeignKey(x => x.MentorId)
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

        private static UserAccount CreateSeedUser(int id, string email, string firstName, string lastName, Role role, string password)
        {
            var salt = SHA512.HashData(Encoding.UTF8.GetBytes($"RikiPath.Seed.Salt.v1:{email}"));
            using var hmac = new HMACSHA512(salt);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

            return new UserAccount
            {
                Id = id,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                PasswordSalt = salt,
                PasswordHash = hash,
                IsEmailVerified = true,
                IsDeleted = false,
                CreatedDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)
            };
        }
    }
}
