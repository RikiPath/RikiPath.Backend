using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class LearnerPracticeAnswerConfig : IEntityTypeConfiguration<LearnerPracticeAnswer>
{
    public void Configure(EntityTypeBuilder<LearnerPracticeAnswer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.PracticeExerciseAttempt).WithMany(x => x.Answers).HasForeignKey(x => x.PracticeExerciseAttemptId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PracticeQuestion).WithMany(x => x.Answers).HasForeignKey(x => x.PracticeQuestionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SelectedOption).WithMany().HasForeignKey(x => x.SelectedOptionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.PracticeExerciseAttemptId, x.PracticeQuestionId }).IsUnique();
    }
}
