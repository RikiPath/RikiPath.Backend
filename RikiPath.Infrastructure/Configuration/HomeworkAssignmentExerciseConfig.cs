using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class HomeworkAssignmentExerciseConfig : IEntityTypeConfiguration<HomeworkAssignmentExercise>
{
    public void Configure(EntityTypeBuilder<HomeworkAssignmentExercise> builder)
    {
        builder.ToTable("HomeworkAssignmentExercises");
        builder.HasKey(x => new { x.HomeworkAssignmentId, x.PracticeExerciseId });
        builder.HasOne(x => x.HomeworkAssignment).WithMany(x => x.Exercises)
            .HasForeignKey(x => x.HomeworkAssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PracticeExercise).WithMany()
            .HasForeignKey(x => x.PracticeExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}
