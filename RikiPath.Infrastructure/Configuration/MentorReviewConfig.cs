using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorReviewConfig : IEntityTypeConfiguration<MentorReview>
{
    public void Configure(EntityTypeBuilder<MentorReview> b)
    {
        b.ToTable("MentorReviews", t =>
        {
            t.HasCheckConstraint("CK_MentorReview_Rating_Range", "\"Rating\" >= 1 AND \"Rating\" <= 5");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Rating)
            .IsRequired();

        b.Property(x => x.Comment)
            .HasMaxLength(1000);

        b.Property(x => x.MentorReply)
            .HasMaxLength(1000);

        b.Property(x => x.ModerationStatus)
            .IsRequired()
            .HasConversion<int>();

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        // Mỗi lịch hẹn chỉ được đánh giá tối đa 1 lần (nguyên tắc chống trùng lặp tuyệt đối)
        b.HasIndex(x => x.MentorBookingId)
            .IsUnique()
            .HasDatabaseName("IX_MentorReviews_MentorBookingId");

        b.HasIndex(x => new { x.MentorId, x.IsPublic, x.ModerationStatus })
            .HasDatabaseName("IX_MentorReviews_Mentor_Public_Status");

        b.HasIndex(x => x.LearnerId)
            .HasDatabaseName("IX_MentorReviews_LearnerId");

        b.HasOne(x => x.MentorBooking)
            .WithMany()
            .HasForeignKey(x => x.MentorBookingId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Learner)
            .WithMany()
            .HasForeignKey(x => x.LearnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
