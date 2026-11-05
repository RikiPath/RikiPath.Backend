using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class ContentAiReviewConfig : IEntityTypeConfiguration<ContentAiReview>
    {
        public void Configure(EntityTypeBuilder<ContentAiReview> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ContentType).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.Verdict).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Summary).HasMaxLength(2000);
            builder.Property(x => x.IssuesJson).IsRequired();

            // Tra cứu nhanh lịch sử review theo loại nội dung, Id và thời gian review.
            builder.HasIndex(x => new { x.ContentType, x.ContentId, x.ReviewedAt });
        }
    }
}