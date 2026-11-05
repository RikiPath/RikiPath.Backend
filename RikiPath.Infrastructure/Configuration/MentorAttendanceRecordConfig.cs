using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorAttendanceRecordConfig : IEntityTypeConfiguration<MentorAttendanceRecord>
{
    public void Configure(EntityTypeBuilder<MentorAttendanceRecord> b)
    {
        b.ToTable("MentorAttendanceRecords", t =>
        {
            t.HasCheckConstraint("CK_MentorAttendanceRecord_Reconnection", "\"ReconnectionCount\" >= 0");
            t.HasCheckConstraint("CK_MentorAttendanceRecord_Duration", "\"TotalDurationSeconds\" >= 0");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Role)
            .IsRequired()
            .HasMaxLength(32);

        b.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        // Chỉ mục duy nhất: mỗi booking chỉ có đúng một bản ghi điểm danh cho một người tham gia
        b.HasIndex(x => new { x.MentorBookingId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_MentorAttendanceRecords_Booking_User");

        b.HasIndex(x => x.RoomId)
            .HasDatabaseName("IX_MentorAttendanceRecords_RoomId");

        b.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_MentorAttendanceRecords_UserId");

        b.HasIndex(x => x.Status)
            .HasDatabaseName("IX_MentorAttendanceRecords_Status");

        b.HasOne(x => x.MentorBooking)
            .WithMany()
            .HasForeignKey(x => x.MentorBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.UserAccount)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
