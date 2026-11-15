using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorDisputeConfig : IEntityTypeConfiguration<MentorDispute>
{
    public void Configure(EntityTypeBuilder<MentorDispute> b)
    {
        b.ToTable("MentorDisputes");

        b.HasKey(x => x.Id);

        b.Property(x => x.Category)
            .IsRequired()
            .HasConversion<int>();

        b.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        b.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        b.Property(x => x.EvidenceUrls)
            .HasMaxLength(2000);

        b.Property(x => x.MentorResponse)
            .HasMaxLength(1000);

        b.Property(x => x.AdminNotes)
            .HasMaxLength(1000);

        b.Property(x => x.AdminDecision)
            .HasMaxLength(100);

        b.Property(x => x.RefundOperationKey)
            .HasMaxLength(128);

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        b.HasIndex(x => x.MentorBookingId)
            .HasDatabaseName("IX_MentorDisputes_MentorBookingId");

        b.HasIndex(x => x.LearnerId)
            .HasDatabaseName("IX_MentorDisputes_LearnerId");

        b.HasIndex(x => x.MentorId)
            .HasDatabaseName("IX_MentorDisputes_MentorId");

        b.HasIndex(x => x.Status)
            .HasDatabaseName("IX_MentorDisputes_Status");

        b.HasOne(x => x.MentorBooking)
            .WithMany()
            .HasForeignKey(x => x.MentorBookingId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Learner)
            .WithMany()
            .HasForeignKey(x => x.LearnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ResolvedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ResolvedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
