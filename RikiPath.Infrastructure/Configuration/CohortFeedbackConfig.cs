using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class CohortMentorReviewConfig : IEntityTypeConfiguration<CohortMentorReview>
{
    public void Configure(EntityTypeBuilder<CohortMentorReview> b)
    {
        b.ToTable("CohortMentorReviews", t =>
        {
            t.HasCheckConstraint("CK_CohortMentorReview_Rating", "\"Rating\" BETWEEN 1 AND 5");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.CourseId, x.MentorId }).IsUnique();
        b.HasIndex(x => new { x.MentorId, x.IsHidden, x.CreatedAtUtc });
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cohort).WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CohortComplaintConfig : IEntityTypeConfiguration<CohortComplaint>
{
    public void Configure(EntityTypeBuilder<CohortComplaint> b)
    {
        b.ToTable("CohortComplaints", t =>
        {
            t.HasCheckConstraint("CK_CohortComplaint_Category",
                "\"Category\" IN ('TeachingQuality', 'MentorConduct', 'Schedule', 'PaymentEnrollment', 'Other')");
            t.HasCheckConstraint("CK_CohortComplaint_Status",
                "\"Status\" IN ('Open', 'UnderReview', 'Resolved', 'Rejected')");
            t.HasCheckConstraint("CK_CohortComplaint_Message",
                "length(btrim(\"Message\")) BETWEEN 10 AND 2000");
            t.HasCheckConstraint("CK_CohortComplaint_RefundReview",
                "NOT \"RefundReviewRequested\" OR \"Category\" = 'PaymentEnrollment'");
            t.HasCheckConstraint("CK_CohortComplaint_Decision",
                "(\"Status\" NOT IN ('Resolved', 'Rejected') OR (\"Decision\" IS NOT NULL AND length(btrim(\"Decision\")) > 0 AND \"DecidedById\" IS NOT NULL AND \"DecidedAtUtc\" IS NOT NULL))");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasMaxLength(30).IsRequired();
        b.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        b.Property(x => x.EvidenceUrl).HasMaxLength(500);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.MentorResponse).HasMaxLength(2000);
        b.Property(x => x.Decision).HasMaxLength(2000);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.MentorRespondedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.DecidedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.LearnerId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        b.HasIndex(x => new { x.MentorId, x.Status, x.CreatedAtUtc });
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cohort).WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne(x => x.Complaint).HasForeignKey(x => x.ComplaintId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CohortComplaintEventConfig : IEntityTypeConfiguration<CohortComplaintEvent>
{
    public void Configure(EntityTypeBuilder<CohortComplaintEvent> b)
    {
        b.ToTable("CohortComplaintEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(40).IsRequired();
        b.Property(x => x.ActorRole).HasMaxLength(20).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(2000).IsRequired();
        b.Property(x => x.AtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.ComplaintId, x.Id });
    }
}
