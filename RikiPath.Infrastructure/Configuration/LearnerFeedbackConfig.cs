using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class LearnerFeedbackConfig : IEntityTypeConfiguration<LearnerFeedback>
{
    public void Configure(EntityTypeBuilder<LearnerFeedback> builder)
    {
        builder.ToTable("LearnerFeedback", table =>
        {
            table.HasCheckConstraint("CK_LearnerFeedback_Rating", "\"Rating\" BETWEEN 1 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).HasMaxLength(32);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.HasIndex(x => new { x.LearnerId, x.TargetType, x.TargetId }).IsUnique();
        builder.HasIndex(x => new { x.UpdatedAtUtc, x.Id });
        builder.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
