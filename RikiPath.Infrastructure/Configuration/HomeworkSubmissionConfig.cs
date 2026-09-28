using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class HomeworkSubmissionConfig : IEntityTypeConfiguration<HomeworkSubmission>
{
    public void Configure(EntityTypeBuilder<HomeworkSubmission> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.HomeworkAssignment).WithOne(x => x.Submission).HasForeignKey<HomeworkSubmission>(x => x.HomeworkAssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.HomeworkAssignmentId).IsUnique();
    }
}
