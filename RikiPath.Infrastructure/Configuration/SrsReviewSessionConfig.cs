using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class SrsReviewSessionConfig : IEntityTypeConfiguration<SrsReviewSession>
{
    public void Configure(EntityTypeBuilder<SrsReviewSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudyMode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SessionKind).IsRequired().HasMaxLength(20).HasDefaultValue("Scheduled");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.FilterCardType).HasMaxLength(30);
        builder.Property(x => x.FilterJlptLevel).HasMaxLength(10);
        builder.Property(x => x.FilterSourceType).HasMaxLength(40);
        builder.Property(x => x.FilterLearningState).HasMaxLength(20);
        builder.Property(x => x.SelectionScope).HasMaxLength(30);
        builder.Property(x => x.QueueCardIdsJson).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.CompletedCardIdsJson).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.RetryCardIdsJson).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.ProcessedStepKeysJson).IsRequired().HasMaxLength(8000).HasDefaultValue("[]");

        builder.HasOne(x => x.UserAccount)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SessionKey).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.LastActivityAtUtc });
        builder.HasIndex(x => new { x.UserId, x.SessionKind, x.Status, x.LastActivityAtUtc });
    }
}
