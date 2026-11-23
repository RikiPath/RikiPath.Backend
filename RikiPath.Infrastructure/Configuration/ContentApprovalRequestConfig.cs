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
            builder.Property(x => x.SourceType).IsRequired().HasMaxLength(40).HasDefaultValue("ImportExcel");
            builder.Property(x => x.SourceReference).HasMaxLength(300);
            builder.Property(x => x.RightsStatus).HasMaxLength(100);
            builder.Property(x => x.ValidationSummaryJson).HasColumnType("jsonb");
            builder.Property(x => x.AiProvenanceJson).HasColumnType("jsonb");
            builder.Property(x => x.ReviewHistoryJson).HasColumnType("jsonb");
            builder.Property(x => x.IdempotencyKey).HasMaxLength(100);

            // Tra cứu nhanh theo lần import (xem lại 1 batch) và theo loại nội dung đang chờ duyệt.
            builder.HasIndex(x => x.BatchId);
            builder.HasIndex(x => new { x.EntityType, x.Status });
            builder.HasIndex(x => x.SubmittedByUserId);
            builder.HasIndex(x => new { x.SubmittedByUserId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");

            builder.HasOne(x => x.SubmittedBy)
                .WithMany()
                .HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
