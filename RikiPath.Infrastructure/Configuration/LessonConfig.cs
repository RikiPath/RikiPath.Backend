using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LessonConfig : IEntityTypeConfiguration<Lesson>
    {
        public void Configure(EntityTypeBuilder<Lesson> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CertificateLevelId).HasColumnName("CertificationLevelId");
            builder.Property(x => x.LanguageSkillId).HasColumnName("SkillId");
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);

            builder.HasOne(x => x.CertificateLevel)
                .WithMany(x => x.Lessons)
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.LanguageSkill)
                .WithMany(x => x.Lessons)
                .HasForeignKey(x => x.LanguageSkillId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificateLevelId, x.LanguageSkillId });
            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(RikiPath.Domain.Enums.ContentStatus.Published)
                .HasSentinel((RikiPath.Domain.Enums.ContentStatus)(-1));
            builder.Property(x => x.Version).HasDefaultValue(1).HasSentinel(-1);
            builder.Property(x => x.EstimatedMinutes).HasDefaultValue(15).HasSentinel(-1);
            builder.Property(x => x.LearningObjectives).HasMaxLength(1000);
            builder.Property(x => x.SourceProvenance).HasMaxLength(300);
            builder.HasOne(x => x.PrerequisiteLesson)
                .WithMany()
                .HasForeignKey(x => x.PrerequisiteLessonId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Property(x => x.UnpublishedReason).HasMaxLength(500);
            builder.HasIndex(x => x.Status);
        }
    }
}
