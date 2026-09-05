using Domain.Entities;
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
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

            // Learner's target JLPT level — optional, does not cascade-delete the user
            builder.HasOne(x => x.TargetJlptLevel)
                .WithMany(x => x.LearnersTargeting)
                .HasForeignKey(x => x.TargetJlptLevelId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(x => x.AuthoredCourses)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ReviewedCourses)
                .WithOne(x => x.ReviewedBy)
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredKanjiEntries)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ReviewedKanjiEntries)
                .WithOne(x => x.ReviewedBy)
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredVocabularyEntries)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ReviewedVocabularyEntries)
                .WithOne(x => x.ReviewedBy)
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredGrammarPoints)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ReviewedGrammarPoints)
                .WithOne(x => x.ReviewedBy)
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.AuthoredPracticeTests)
                .WithOne(x => x.ContentAuthor)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ReviewedPracticeTests)
                .WithOne(x => x.ReviewedBy)
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

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

            builder.HasMany(x => x.Notifications)
                .WithOne(x => x.UserAccount)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
