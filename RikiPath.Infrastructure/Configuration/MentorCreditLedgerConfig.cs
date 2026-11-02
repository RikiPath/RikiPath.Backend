using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

// Cấu hình bảng nguồn cấp tín dụng MentorCreditGrants.
public sealed class MentorCreditGrantConfig : IEntityTypeConfiguration<MentorCreditGrant>
{
    public void Configure(EntityTypeBuilder<MentorCreditGrant> b)
    {
        b.ToTable("MentorCreditGrants", t =>
        {
            t.HasCheckConstraint("CK_MentorCreditGrant_Quantity", "\"InitialQuantity\" > 0 AND \"AvailableQuantity\" >= 0 AND \"AvailableQuantity\" <= \"InitialQuantity\"");
            t.HasCheckConstraint("CK_MentorCreditGrant_PolicyVersion", "\"PolicyVersion\" > 0");
        });

        b.HasKey(x => x.Id);

        b.HasOne(x => x.Learner)
            .WithMany()
            .HasForeignKey(x => x.LearnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.UserSubscription)
            .WithMany()
            .HasForeignKey(x => x.UserSubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        b.Property(x => x.Version).IsRowVersion();

        // Một gói thanh toán chỉ được cấp một nguồn tín dụng duy nhất.
        b.HasIndex(x => x.UserSubscriptionId).IsUnique();

        // Chỉ mục phục vụ truy vấn các nguồn cấp còn hạn và còn số dư theo FIFO.
        b.HasIndex(x => new { x.LearnerId, x.ExpiresAtUtc });
    }
}

// Cấu hình bảng bút toán sổ cái tín dụng MentorCreditEntries.
public sealed class MentorCreditEntryConfig : IEntityTypeConfiguration<MentorCreditEntry>
{
    public void Configure(EntityTypeBuilder<MentorCreditEntry> b)
    {
        b.ToTable("MentorCreditEntries", t =>
        {
            t.HasCheckConstraint("CK_MentorCreditEntry_Amount", "\"Amount\" <> 0");
        });

        b.HasKey(x => x.Id);

        b.HasOne(x => x.CreditGrant)
            .WithMany(g => g.Entries)
            .HasForeignKey(x => x.CreditGrantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Learner)
            .WithMany()
            .HasForeignKey(x => x.LearnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.MentorBooking)
            .WithMany()
            .HasForeignKey(x => x.MentorBookingId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.OperationKey).HasMaxLength(160).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        // Ràng buộc duy nhất trên OperationKey đảm bảo tính bất biến và chống trùng lắp hoàn toàn (Idempotent).
        b.HasIndex(x => x.OperationKey).IsUnique();

        // Chỉ mục hỗ trợ kiểm tra bút toán theo buổi hẹn và loại giao dịch.
        b.HasIndex(x => new { x.MentorBookingId, x.EntryType });
        b.HasIndex(x => new { x.CreditGrantId, x.EntryType });
    }
}
