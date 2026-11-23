using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ReviewHistoryConfig : IEntityTypeConfiguration<ReviewHistory>
    {
        public void Configure(EntityTypeBuilder<ReviewHistory> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ReviewCardId).HasColumnName("ReviewItemId");
            builder.Property(x => x.Rating).HasConversion<string>().HasMaxLength(10);
            builder.Property(x => x.StudyMode).IsRequired().HasMaxLength(20).HasDefaultValue("flip");
            builder.Property(x => x.UserAnswer).HasMaxLength(500);
            builder.Property(x => x.PreviousState).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RikiPath.Domain.Enums.SrsLearningState.New).HasSentinel((RikiPath.Domain.Enums.SrsLearningState)(-1));
            builder.Property(x => x.NewState).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RikiPath.Domain.Enums.SrsLearningState.Learning).HasSentinel((RikiPath.Domain.Enums.SrsLearningState)(-1));
            builder.Property(x => x.PreviousEaseFactor).HasDefaultValue(2.5);
            builder.Property(x => x.NewEaseFactor).HasDefaultValue(2.5);
            builder.Property(x => x.IdempotencyKey).HasMaxLength(120);
            builder.Property(x => x.SessionReference).HasMaxLength(100);

            builder.HasOne(x => x.ReviewCard)
                .WithMany(x => x.ReviewHistories)
                .HasForeignKey(x => x.ReviewCardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.ReviewCardId, x.ReviewedAt });
            builder.HasIndex(x => x.SessionReference);
            builder.HasIndex(x => new { x.ReviewCardId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        }
    }
}
