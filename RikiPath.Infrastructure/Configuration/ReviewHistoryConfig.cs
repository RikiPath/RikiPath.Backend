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

            builder.HasOne(x => x.ReviewCard)
                .WithMany(x => x.ReviewHistories)
                .HasForeignKey(x => x.ReviewCardId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
