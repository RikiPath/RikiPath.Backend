using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorTeachingProposalConfig : IEntityTypeConfiguration<MentorTeachingProposal>
{
    public void Configure(EntityTypeBuilder<MentorTeachingProposal> b)
    {
        b.ToTable("MentorTeachingProposals", t =>
        {
            t.HasCheckConstraint(
                "CK_MentorTeachingProposal_Status",
                "\"Status\" IN ('Draft', 'PendingReview', 'ChangesRequested', 'Approved', 'Rejected', 'Withdrawn')");
            t.HasCheckConstraint(
                "CK_MentorTeachingProposal_ProgramType",
                "\"ProgramType\" IN ('StandardJlpt', 'IntensiveJlpt', 'ExamPrepJlpt', 'CustomJlpt')");
            t.HasCheckConstraint(
                "CK_MentorTeachingProposal_Duration",
                "\"CourseDurationWeeks\" BETWEEN 1 AND 52 AND \"SessionDurationMinutes\" BETWEEN 30 AND 240");
            t.HasCheckConstraint(
                "CK_MentorTeachingProposal_Revision",
                "\"Revision\" > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.ProgramType).HasMaxLength(32).IsRequired();
        b.Property(x => x.CourseTitle).HasMaxLength(200);
        b.Property(x => x.CourseObjective).HasMaxLength(1500);
        b.Property(x => x.ExpectedStartDate).HasColumnType("date");
        b.Property(x => x.WeeklySessionsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.OptionalNotes).HasMaxLength(2000);
        b.Property(x => x.OptionalAttachmentsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.SubmissionIdempotencyKey).HasMaxLength(128);
        b.Property(x => x.ReviewReason).HasMaxLength(1500);

        b.Property(x => x.SubmittedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ReviewedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.LastSavedAtUtc).HasColumnType("timestamp with time zone");

        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CoursePlan).WithMany().HasForeignKey(x => x.CoursePlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReviewedByAdmin).WithMany().HasForeignKey(x => x.ReviewedByAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.GroupPolicy).WithMany().HasForeignKey(x => x.GroupPolicyId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Events)
            .WithOne(e => e.Proposal)
            .HasForeignKey(e => e.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.MentorId, x.Status, x.UpdatedAtUtc });
        b.HasIndex(x => new { x.Status, x.SubmittedAtUtc });
        b.HasIndex(x => new { x.MentorId, x.SubmissionIdempotencyKey })
            .IsUnique()
            .HasDatabaseName("IX_MentorTeachingProposals_MentorId_SubmissionIdempotencyKey")
            .HasFilter("\"SubmissionIdempotencyKey\" IS NOT NULL");
    }
}

public sealed class MentorTeachingProposalEventConfig : IEntityTypeConfiguration<MentorTeachingProposalEvent>
{
    public void Configure(EntityTypeBuilder<MentorTeachingProposalEvent> b)
    {
        b.ToTable("MentorTeachingProposalEvents", t =>
        {
            t.HasCheckConstraint("CK_MentorTeachingProposalEvent_RevisionAfter", "\"RevisionAfter\" > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.ActorRole).HasMaxLength(32).IsRequired();
        b.Property(x => x.Action).HasMaxLength(48).IsRequired();
        b.Property(x => x.FromStatus).HasMaxLength(32);
        b.Property(x => x.ToStatus).HasMaxLength(32);
        b.Property(x => x.Reason).HasMaxLength(1500);
        b.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        b.HasIndex(x => new { x.ProposalId, x.CreatedAtUtc });
    }
}
