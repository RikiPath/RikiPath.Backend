using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class ContentApprovalRequestConfig : IEntityTypeConfiguration<ContentApprovalRequest>
    {
        public void Configure(EntityTypeBuilder<ContentApprovalRequest> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.RowKey).HasMaxLength(500).IsRequired();
            builder.Property(x => x.SourceFileName).HasMaxLength(260);
            builder.Property(x => x.PayloadJson).IsRequired();

            // Tra cứu nhanh theo lần import (xem lại 1 batch) và theo loại nội dung đang chờ duyệt.
            builder.HasIndex(x => x.BatchId);
            builder.HasIndex(x => new { x.EntityType, x.Status });

            builder.HasOne(x => x.SubmittedBy)
                .WithMany()
                .HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
