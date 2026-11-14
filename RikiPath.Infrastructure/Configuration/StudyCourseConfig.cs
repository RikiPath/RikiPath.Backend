using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class StudyCourseConfig : IEntityTypeConfiguration<StudyCourse>
{
    public void Configure(EntityTypeBuilder<StudyCourse> b)
    {
        b.ToTable("StudyCourses"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Amount).HasPrecision(18, 0);
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RetakeOf).WithMany().HasForeignKey(x => x.RetakeOfId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RetakeOfId).IsUnique();
        b.HasIndex(x => new { x.LearnerId, x.PurchaseKey }).IsUnique();
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
    }
}

public sealed class CourseAuditEntryConfig : IEntityTypeConfiguration<CourseAuditEntry>
{
    public void Configure(EntityTypeBuilder<CourseAuditEntry> b)
    { b.ToTable("CourseAuditEntries"); b.HasKey(x => x.Id); b.Property(x => x.Detail).HasMaxLength(2000); }
}
