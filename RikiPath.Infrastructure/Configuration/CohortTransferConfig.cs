using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class CohortTransferRequestConfig : IEntityTypeConfiguration<CohortTransferRequest>
{
    public void Configure(EntityTypeBuilder<CohortTransferRequest> b)
    {
        b.ToTable("CohortTransferRequests", t =>
        {
            t.HasCheckConstraint("CK_CohortTransferRequest_Status",
                "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
            // Duyệt phải có lớp đích + ghi danh đích; từ chối phải có lý do.
            t.HasCheckConstraint("CK_CohortTransferRequest_Decision",
                "(\"Status\" <> 'Approved' OR (\"TargetCohortId\" IS NOT NULL AND \"DestinationCourseId\" IS NOT NULL AND \"ReviewedById\" IS NOT NULL)) AND " +
                "(\"Status\" <> 'Rejected' OR (\"DecisionNote\" IS NOT NULL AND length(btrim(\"DecisionNote\")) > 0 AND \"ReviewedById\" IS NOT NULL))");
            t.HasCheckConstraint("CK_CohortTransferRequest_Target",
                "\"TargetCohortId\" IS NULL OR \"TargetCohortId\" <> \"SourceCohortId\"");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.EvidenceUrl).HasMaxLength(500);
        b.Property(x => x.DecisionNote).HasMaxLength(1000);
        b.Property(x => x.EligibilitySnapshot).HasColumnType("text");
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ReviewedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.LearnerId, x.IdempotencyKey }).IsUnique();
        // Mỗi ghi danh nguồn chỉ có tối đa một yêu cầu đang chờ.
        b.HasIndex(x => x.SourceCourseId).IsUnique()
            .HasDatabaseName("IX_CohortTransferRequests_PendingSource")
            .HasFilter("\"Status\" = 'Pending'");
        // Một ghi danh đích chỉ sinh từ đúng một yêu cầu đã duyệt.
        b.HasIndex(x => x.DestinationCourseId).IsUnique().HasFilter("\"DestinationCourseId\" IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceCourse).WithMany().HasForeignKey(x => x.SourceCourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceCohort).WithMany().HasForeignKey(x => x.SourceCohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RequestedTargetCohort).WithMany().HasForeignKey(x => x.RequestedTargetCohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TargetCohort).WithMany().HasForeignKey(x => x.TargetCohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DestinationCourse).WithMany().HasForeignKey(x => x.DestinationCourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MentorGroupPolicy>().WithMany().HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne(x => x.Request).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CohortTransferRequestEventConfig : IEntityTypeConfiguration<CohortTransferRequestEvent>
{
    public void Configure(EntityTypeBuilder<CohortTransferRequestEvent> b)
    {
        b.ToTable("CohortTransferRequestEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(40);
        b.Property(x => x.ActorRole).HasMaxLength(20);
        b.Property(x => x.Detail).HasMaxLength(2000);
        b.Property(x => x.AtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.RequestId, x.Id });
    }
}

public sealed class CohortScheduleRequestConfig : IEntityTypeConfiguration<CohortScheduleRequest>
{
    public void Configure(EntityTypeBuilder<CohortScheduleRequest> b)
    {
        b.ToTable("CohortScheduleRequests", t =>
        {
            t.HasCheckConstraint("CK_CohortScheduleRequest_Kind", "\"Kind\" IN ('ShiftWeeks', 'CancelEmptyCohort')");
            t.HasCheckConstraint("CK_CohortScheduleRequest_Status",
                "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')");
            t.HasCheckConstraint("CK_CohortScheduleRequest_ShiftWeeks",
                "(\"Kind\" = 'ShiftWeeks' AND \"ShiftWeeks\" BETWEEN 1 AND 8) OR (\"Kind\" = 'CancelEmptyCohort' AND \"ShiftWeeks\" IS NULL)");
            t.HasCheckConstraint("CK_CohortScheduleRequest_Reason",
                "length(btrim(\"Reason\")) BETWEEN 10 AND 1000");
            t.HasCheckConstraint("CK_CohortScheduleRequest_Decision",
                "(\"Status\" NOT IN ('Approved', 'Rejected') OR (\"ReviewedById\" IS NOT NULL AND \"ReviewedAtUtc\" IS NOT NULL)) AND " +
                "(\"Status\" <> 'Rejected' OR (\"DecisionNote\" IS NOT NULL AND length(btrim(\"DecisionNote\")) > 0))");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(30);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.RequestedByRole).HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.DecisionNote).HasMaxLength(1000);
        b.Property(x => x.ImpactSnapshot).HasColumnType("text");
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ReviewedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.RequestedById, x.IdempotencyKey }).IsUnique();
        // Mỗi lớp chỉ có tối đa một yêu cầu dời/hủy đang chờ.
        b.HasIndex(x => x.CohortId).IsUnique()
            .HasDatabaseName("IX_CohortScheduleRequests_PendingCohort")
            .HasFilter("\"Status\" = 'Pending'");
        b.HasIndex(x => new { x.CohortId, x.Id });
        b.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        b.HasOne(x => x.Cohort).WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne(x => x.Request).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CohortScheduleRequestEventConfig : IEntityTypeConfiguration<CohortScheduleRequestEvent>
{
    public void Configure(EntityTypeBuilder<CohortScheduleRequestEvent> b)
    {
        b.ToTable("CohortScheduleRequestEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(40);
        b.Property(x => x.ActorRole).HasMaxLength(20);
        b.Property(x => x.Detail).HasMaxLength(2000);
        b.Property(x => x.AtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.RequestId, x.Id });
    }
}

public sealed class StudyCohortMentorAssignmentConfig : IEntityTypeConfiguration<StudyCohortMentorAssignment>
{
    public void Configure(EntityTypeBuilder<StudyCohortMentorAssignment> b)
    {
        b.ToTable("StudyCohortMentorAssignments", t =>
        {
            t.HasCheckConstraint("CK_StudyCohortMentorAssignment_DifferentMentor", "\"MentorId\" <> \"PreviousMentorId\"");
            t.HasCheckConstraint("CK_StudyCohortMentorAssignment_Reason", "length(btrim(\"Reason\")) > 0");
            t.HasCheckConstraint("CK_StudyCohortMentorAssignment_Sessions", "\"EffectiveFromSessionNumber\" > 0 AND \"AffectedSessionCount\" > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ImpactSnapshot).HasColumnType("text");
        b.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.CohortId, x.Id });
        b.HasOne(x => x.Cohort).WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.PreviousMentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MentorGroupPolicy>().WithMany().HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}
