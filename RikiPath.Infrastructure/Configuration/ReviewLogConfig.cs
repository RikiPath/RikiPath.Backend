using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ReviewLogConfig : IEntityTypeConfiguration<ReviewLog>
    {
        public void Configure(EntityTypeBuilder<ReviewLog> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Rating).HasConversion<string>().HasMaxLength(10);

            builder.HasOne(x => x.ReviewItem)
                .WithMany(x => x.ReviewLogs)
                .HasForeignKey(x => x.ReviewItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
