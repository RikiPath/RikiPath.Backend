using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorScheduleConfig : IEntityTypeConfiguration<MentorSchedule>
{
    public void Configure(EntityTypeBuilder<MentorSchedule> b)
    {
        b.ToTable("MentorSchedules", t => t.HasCheckConstraint("CK_MentorSchedule_PolicyVersion", "\"PolicyVersion\" > 0"));
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        b.Property(x => x.EffectiveFrom).HasColumnType("date");
        b.Property(x => x.GeneratedThrough).HasColumnType("date");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.MentorId, x.PolicyVersion }).IsUnique();
    }
}

public sealed class MentorScheduleRuleConfig : IEntityTypeConfiguration<MentorScheduleRule>
{
    public void Configure(EntityTypeBuilder<MentorScheduleRule> b)
    {
        b.ToTable("MentorScheduleRules", t =>
        {
            t.HasCheckConstraint("CK_MentorScheduleRule_Weekday", "\"Weekday\" BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_MentorScheduleRule_Time", "\"LocalEnd\" > \"LocalStart\"");
            t.HasCheckConstraint("CK_MentorScheduleRule_Daypart", "\"DaypartCode\" IN ('Morning', 'Noon', 'Afternoon', 'Evening')");
        });
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Schedule).WithMany(x => x.Rules).HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.DaypartCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.LocalStart).HasColumnType("time without time zone");
        b.Property(x => x.LocalEnd).HasColumnType("time without time zone");
        b.Property(x => x.EffectiveFrom).HasColumnType("date");
        b.Property(x => x.EffectiveTo).HasColumnType("date");
        b.HasIndex(x => new { x.ScheduleId, x.Weekday, x.DaypartCode, x.EffectiveFrom }).IsUnique();
    }
}

public sealed class MentorBookingIntentConfig : IEntityTypeConfiguration<MentorBookingIntent>
{
    public void Configure(EntityTypeBuilder<MentorBookingIntent> b)
    {
        b.ToTable("MentorBookingIntents");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MentorBooking).WithMany().HasForeignKey(x => x.MentorBookingId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.IntentKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.LearnerId, x.IntentKey }).IsUnique();
    }
}

public sealed class MentorBookingTermsConfig : IEntityTypeConfiguration<MentorBookingTerms>
{
    public void Configure(EntityTypeBuilder<MentorBookingTerms> b)
    {
        b.ToTable("MentorBookingTerms", t =>
        {
            t.HasCheckConstraint("CK_MentorBookingTerms_Interval", "\"ScheduledEndUtc\" > \"ScheduledStartUtc\"");
            t.HasCheckConstraint("CK_MentorBookingTerms_PolicyVersion", "\"PolicyVersion\" > 0");
        });
        b.HasKey(x => x.MentorBookingId);
        b.HasOne(x => x.MentorBooking).WithOne().HasForeignKey<MentorBookingTerms>(x => x.MentorBookingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MentorAvailability).WithMany().HasForeignKey(x => x.MentorAvailabilityId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ScheduledStartUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ScheduledEndUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ConfirmedAtUtc).HasColumnType("timestamp with time zone");
        // Buổi đã hủy vẫn được giữ trong lịch sử; quyền giữ khung giờ hiện tại nằm ở bản ghi giữ chỗ chưa nhả.
        b.HasIndex(x => x.MentorAvailabilityId);
    }
}

public sealed class MentorBookingReservationConfig : IEntityTypeConfiguration<MentorBookingReservation>
{
    public void Configure(EntityTypeBuilder<MentorBookingReservation> b)
    {
        b.ToTable("MentorBookingReservations", t =>
        {
            t.HasCheckConstraint("CK_MentorBookingReservation_Interval", "\"EndUtc\" > \"StartUtc\"");
            t.HasCheckConstraint("CK_MentorBookingReservation_Rest", "\"RestProtectedUntilUtc\" = \"EndUtc\" + INTERVAL '15 minutes'");
            t.HasCheckConstraint("CK_MentorBookingReservation_PolicyVersion", "\"PolicyVersion\" > 0");
        });
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MentorBooking).WithMany().HasForeignKey(x => x.MentorBookingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MentorAvailability).WithMany()
            .HasForeignKey(x => new { x.MentorAvailabilityId, x.MentorId })
            .HasPrincipalKey(x => new { x.Id, x.MentorId })
            .OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.LocalBusinessDate).HasColumnType("date");
        b.Property(x => x.StartUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.EndUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.RestProtectedUntilUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ReleasedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.MentorBookingId).IsUnique();
        // Mỗi khung giờ chỉ có một bản ghi giữ chỗ đang hoạt động; bản ghi đã nhả vẫn được giữ để đối soát.
        b.HasIndex(x => x.MentorAvailabilityId).IsUnique().HasFilter("\"ReleasedAtUtc\" IS NULL");
        b.HasIndex(x => new { x.MentorId, x.LocalBusinessDate, x.ReleasedAtUtc });
        // Ràng buộc loại trừ GiST EX_MentorBookingReservation_ActiveMentorRest bảo vệ khoảng [StartUtc, RestProtectedUntilUtc)
        // khi ReleasedAtUtc IS NULL; yêu cầu PostgreSQL extension btree_gist đã được tích hợp điều kiện trong migration draft.
    }
}

public sealed class MentorBookingDayGuardConfig : IEntityTypeConfiguration<MentorBookingDayGuard>
{
    public void Configure(EntityTypeBuilder<MentorBookingDayGuard> b)
    {
        b.ToTable("MentorBookingDayGuards", t => t.HasCheckConstraint("CK_MentorBookingDayGuard_Count", "\"ActiveBookingCount\" >= 0"));
        b.HasKey(x => new { x.MentorId, x.LocalBusinessDate });
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.LocalBusinessDate).HasColumnType("date");
    }
}

public sealed class MentorOutboxEventConfig : IEntityTypeConfiguration<MentorOutboxEvent>
{
    public void Configure(EntityTypeBuilder<MentorOutboxEvent> b)
    {
        b.ToTable("MentorOutboxEvents", t =>
        {
            t.HasCheckConstraint("CK_MentorOutbox_Attempts", "\"Attempts\" >= 0");
            t.HasCheckConstraint("CK_MentorOutbox_EventVersion", "\"EventVersion\" > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.EventKey).HasMaxLength(160).IsRequired();
        b.Property(x => x.AggregateType).HasMaxLength(64).IsRequired();
        b.Property(x => x.AggregateId).HasMaxLength(64).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.AvailableAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.DeliveredAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.EventKey).IsUnique();
        b.HasIndex(x => new { x.DeliveredAtUtc, x.AvailableAtUtc, x.CreatedAtUtc });
    }
}
