using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Configuration;

public class PracticeExerciseConfig : IEntityTypeConfiguration<PracticeExercise>
{
    public void Configure(EntityTypeBuilder<PracticeExercise> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Scope)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue(PracticeExerciseScopes.LessonQuickCheck)
            .HasSentinel("");
        builder.Property(x => x.PassingScorePercent)
            .HasDefaultValue(70d)
            .HasSentinel(-1d);
        builder.Property(x => x.LessonId).IsRequired(false);
        builder.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.LanguageSkill).WithMany().HasForeignKey(x => x.LanguageSkillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LessonId, x.LanguageSkillId });
        builder.HasIndex(x => new { x.Scope, x.TopicId });
        builder.HasIndex(x => new { x.Scope, x.CertificateLevelId });
    }
}
