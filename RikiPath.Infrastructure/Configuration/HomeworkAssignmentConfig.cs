using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class HomeworkAssignmentConfig : IEntityTypeConfiguration<HomeworkAssignment>
{
    public void Configure(EntityTypeBuilder<HomeworkAssignment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.Property(x => x.AssignmentScope).IsRequired().HasMaxLength(30).HasDefaultValue("Booking1on1");
        builder.Property(x => x.AiDraftFeedback).HasMaxLength(4000);
        builder.Property(x => x.AiDraftUnavailableReason).HasMaxLength(500);
        builder.Property(x => x.MentorFeedback).HasMaxLength(4000);
        builder.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MentorBooking).WithMany().HasForeignKey(x => x.MentorBookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Cohort).WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StudyCourse).WithMany().HasForeignKey(x => x.StudyCourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LearnerId, x.DueAt });
        builder.HasIndex(x => new { x.CohortId, x.LearnerId });
    }
}
