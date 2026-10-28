using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class RecommendedLearningPathConfig : IEntityTypeConfiguration<RecommendedLearningPath>
    {
        public void Configure(EntityTypeBuilder<RecommendedLearningPath> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SuggestionJson).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            // UserAccount side (Cascade) configured in UserConfig.
            builder.HasIndex(x => new { x.UserId, x.GeneratedAt });
            builder.HasIndex(x => new { x.UserId, x.Version }).IsUnique();
            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'");
        }
    }
}
