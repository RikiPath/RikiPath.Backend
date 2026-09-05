using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LessonProgressConfig : IEntityTypeConfiguration<LessonProgress>
    {
        public void Configure(EntityTypeBuilder<LessonProgress> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Lesson)
                .WithMany(x => x.LessonProgresses)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserId FK/relationship (Cascade) configured from UserConfig.
            builder.HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();
        }
    }
}
