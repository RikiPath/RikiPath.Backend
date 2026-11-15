using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class HomeworkAssignmentLessonConfig : IEntityTypeConfiguration<HomeworkAssignmentLesson>
{
    public void Configure(EntityTypeBuilder<HomeworkAssignmentLesson> builder)
    {
        builder.HasKey(x => new { x.HomeworkAssignmentId, x.LessonId });
        builder.HasOne(x => x.HomeworkAssignment).WithMany(x => x.HomeworkAssignmentLessons)
            .HasForeignKey(x => x.HomeworkAssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Lesson).WithMany(x => x.HomeworkAssignmentLessons)
            .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}
