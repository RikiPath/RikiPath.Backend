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
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
            builder.Property(x => x.VideoUrl).IsRequired().HasMaxLength(1000);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.Course)
                .WithMany(x => x.Lessons)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Skill)
                .WithMany(x => x.Lessons)
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CourseId, x.SortOrder });
        }
    }
}
