using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class UserSrsPreferenceConfig : IEntityTypeConfiguration<UserSrsPreference>
{
    public void Configure(EntityTypeBuilder<UserSrsPreference> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PreferredReviewMode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ReminderFrequency).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ReminderDaysJson).HasMaxLength(100);
        builder.Property(x => x.TimezoneId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PreferredLocale).IsRequired().HasMaxLength(10);

        builder.HasOne(x => x.UserAccount)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => new { x.DailyReminderEnabled, x.LastReminderSentLocalDate });
    }
}
