using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class HomeworkAssignmentConfig : IEntityTypeConfiguration<HomeworkAssignment>
{
    public void Configure(EntityTypeBuilder<HomeworkAssignment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.LearnerId, x.DueAt });
    }
}
