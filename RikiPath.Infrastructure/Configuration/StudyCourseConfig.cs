using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class StudyCourseConfig : IEntityTypeConfiguration<StudyCourse>
{
    public void Configure(EntityTypeBuilder<StudyCourse> b)
    {
        b.ToTable("StudyCourses"); b.HasKey(x => x.Id);
        b.Property(x => x.SessionType).HasDefaultValue("Individual");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Amount).HasPrecision(18, 0);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.OrderSnapshot).HasColumnType("text");
        b.Property(x => x.ProviderPaymentReference).HasMaxLength(120);
        b.Property(x => x.PaymentReviewReason).HasMaxLength(200);
        b.HasIndex(x => x.ProviderPaymentReference)
            .IsUnique()
            .HasDatabaseName("IX_StudyCourses_ProviderPaymentReference")
            .HasFilter("\"ProviderPaymentReference\" IS NOT NULL");
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RetakeOf).WithMany().HasForeignKey(x => x.RetakeOfId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cohort).WithMany(x => x.Enrollments).HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.GroupPolicy).WithMany().HasForeignKey(x => x.GroupPolicyId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.CancellationReason).HasMaxLength(1000);
        b.HasOne<StudyCourse>().WithMany().HasForeignKey(x => x.TransferredFromCourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.TransferredFromCourseId).IsUnique().HasFilter("\"TransferredFromCourseId\" IS NOT NULL");
        b.HasIndex(x => x.CohortId);
        b.HasIndex(x => x.RetakeOfId).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.PurchaseKey }).IsUnique();
        b.HasIndex(x => new { x.CohortId, x.LearnerId })
            .IsUnique()
            .HasDatabaseName("IX_StudyCourses_Cohort_ActiveLearner")
            .HasFilter("\"CohortId\" IS NOT NULL AND \"Status\" = 'Active'");
        b.HasMany(x => x.Sessions).WithOne(x => x.Course).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Homework).WithOne(x => x.Course).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.RetakeRequests).WithOne(x => x.Course).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Audit).WithOne(x => x.Course).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StudyCoursePlanConfig : IEntityTypeConfiguration<StudyCoursePlan>
{
    public void Configure(EntityTypeBuilder<StudyCoursePlan> b)
    {
        b.ToTable("StudyCoursePlans"); b.HasKey(x => x.Id);
        b.Property(x => x.SessionType).HasDefaultValue("Individual"); b.Property(x => x.Capacity).HasDefaultValue(1);
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.Price).HasPrecision(18, 0);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudyCourseSessionConfig : IEntityTypeConfiguration<StudyCourseSession>
{
    public void Configure(EntityTypeBuilder<StudyCourseSession> b)
    {
        b.ToTable("StudyCourseSessions"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CourseId, x.Number, x.Attempt }).IsUnique();
        b.HasOne(x => x.Availability).WithMany().HasForeignKey(x => x.AvailabilityId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.Note).HasMaxLength(1000);
    }
}

public sealed class StudyCourseHomeworkConfig : IEntityTypeConfiguration<StudyCourseHomework>
{
    public void Configure(EntityTypeBuilder<StudyCourseHomework> b)
    {
        b.ToTable("StudyCourseHomework"); b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Submission).HasMaxLength(20000);
    }
}

public sealed class CourseRetakeRequestConfig : IEntityTypeConfiguration<CourseRetakeRequest>
{
    public void Configure(EntityTypeBuilder<CourseRetakeRequest> b)
    {
        b.ToTable("CourseRetakeRequests"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.CourseId).IsUnique().HasFilter("\"Status\" = 'Pending'");
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.MentorSupportPlan).HasMaxLength(2000);
    }
}

public sealed class CohortEnrollmentEntitlementConfig : IEntityTypeConfiguration<CohortEnrollmentEntitlement>
{
    public void Configure(EntityTypeBuilder<CohortEnrollmentEntitlement> b)
    {
        b.ToTable("CohortEnrollmentEntitlements", t =>
        {
            t.HasCheckConstraint(
                "CK_CohortEnrollmentEntitlement_NoCredit",
                "\"SourceType\" NOT IN ('ConsultationCredit', 'Credit1on1', 'OneOnOneCredit')");
            t.HasCheckConstraint(
                "CK_CohortEnrollmentEntitlement_Status",
                "\"Status\" IN ('Active', 'Consumed', 'Revoked', 'Expired')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.EntitlementReference).HasMaxLength(100);
        b.Property(x => x.SourceType).HasMaxLength(40);
        b.Property(x => x.Status).HasMaxLength(24);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.RevocationReason).HasMaxLength(500);
        b.HasIndex(x => x.EntitlementReference).IsUnique();
        b.HasIndex(x => new { x.StudyCohortId, x.LearnerId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Active' AND \"IsDeleted\" = false");
        b.HasOne(x => x.StudyCohort).WithMany().HasForeignKey(x => x.StudyCohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CourseAuditEntryConfig : IEntityTypeConfiguration<CourseAuditEntry>
{
    public void Configure(EntityTypeBuilder<CourseAuditEntry> b)
    { b.ToTable("CourseAuditEntries"); b.HasKey(x => x.Id); b.Property(x => x.Detail).HasMaxLength(2000); }
}

public sealed class StudyCohortConfig : IEntityTypeConfiguration<StudyCohort>
{
    public void Configure(EntityTypeBuilder<StudyCohort> b)
    {
        b.ToTable("StudyCohorts", t =>
        {
            t.HasCheckConstraint("CK_StudyCohort_Capacity", "\"Capacity\" > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.GroupPolicy).WithMany().HasForeignKey(x => x.GroupPolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceProposal).WithMany().HasForeignKey(x => x.SourceProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SourceProposalId).HasFilter("\"SourceProposalId\" IS NOT NULL");
        b.HasMany(x => x.Sessions).WithOne(x => x.Cohort).HasForeignKey(x => x.CohortId);
    }
}
public sealed class StudyCohortSessionConfig : IEntityTypeConfiguration<StudyCohortSession>
{
    public void Configure(EntityTypeBuilder<StudyCohortSession> b)
    {
        b.ToTable("StudyCohortSessions"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.AvailabilityId).IsUnique();
        b.HasIndex(x => new { x.CohortId, x.Number }).IsUnique();
        b.HasOne(x => x.Availability).WithMany().HasForeignKey(x => x.AvailabilityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JlptExamScheduleConfig : IEntityTypeConfiguration<JlptExamSchedule>
{
    public void Configure(EntityTypeBuilder<JlptExamSchedule> b)
    {
        b.ToTable("JlptExamSchedules"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.SourceKey).IsUnique(); b.Property(x => x.SourceKey).HasMaxLength(100);
        b.Property(x => x.Location).HasMaxLength(200); b.Property(x => x.Status).IsConcurrencyToken();
    }
}

public sealed class CourseScheduleRequestConfig : IEntityTypeConfiguration<CourseScheduleRequest>
{
    public void Configure(EntityTypeBuilder<CourseScheduleRequest> b)
    {
        b.ToTable("CourseScheduleRequests"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CourseId, x.SessionId }).IsUnique().HasFilter("\"Status\" = 'Pending'");
        b.Property(x => x.Status).IsConcurrencyToken();
    }
}

public sealed class MentorCalendarConnectionConfig : IEntityTypeConfiguration<MentorCalendarConnection>
{
    public void Configure(EntityTypeBuilder<MentorCalendarConnection> b)
    {
        b.ToTable("MentorCalendarConnections"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.MentorId).IsUnique(); b.HasIndex(x => x.StateHash).IsUnique();
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
    }
}
