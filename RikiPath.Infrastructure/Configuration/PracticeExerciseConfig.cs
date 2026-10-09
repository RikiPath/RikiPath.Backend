using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class PracticeExerciseConfig : IEntityTypeConfiguration<PracticeExercise>
{
    public void Configure(EntityTypeBuilder<PracticeExercise> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReviewedByName).HasMaxLength(200);
        builder.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.ReviewNote).HasMaxLength(2000);
        builder.HasOne(x => x.LanguageSkill).WithMany().HasForeignKey(x => x.LanguageSkillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ContentAuthor).WithMany().HasForeignKey(x => x.ContentAuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LessonId, x.LanguageSkillId, x.Status });
    }
}
