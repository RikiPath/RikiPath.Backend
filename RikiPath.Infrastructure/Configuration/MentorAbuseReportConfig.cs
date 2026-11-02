using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorAbuseReportConfig : IEntityTypeConfiguration<MentorAbuseReport>
{
    public void Configure(EntityTypeBuilder<MentorAbuseReport> b)
    {
        b.ToTable("MentorAbuseReports");

        b.HasKey(x => x.Id);

        b.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(64);

        b.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);

        b.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(32);

        b.Property(x => x.AdminNotes)
            .HasMaxLength(1000);

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.HasIndex(x => x.ReporterId)
            .HasDatabaseName("IX_MentorAbuseReports_ReporterId");

        b.HasIndex(x => x.TargetUserId)
            .HasDatabaseName("IX_MentorAbuseReports_TargetUserId");

        b.HasIndex(x => x.Status)
            .HasDatabaseName("IX_MentorAbuseReports_Status");

        b.HasOne(x => x.Reporter)
            .WithMany()
            .HasForeignKey(x => x.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.TargetUser)
            .WithMany()
            .HasForeignKey(x => x.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.MentorBooking)
            .WithMany()
            .HasForeignKey(x => x.MentorBookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
