using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class PracticeExerciseAttemptConfig : IEntityTypeConfiguration<PracticeExerciseAttempt>
{
    public void Configure(EntityTypeBuilder<PracticeExerciseAttempt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PracticeExercise).WithMany(x => x.Attempts).HasForeignKey(x => x.PracticeExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.PracticeExerciseId, x.StartedAt });
    }
}
