using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class UserDeviceTokenConfig : IEntityTypeConfiguration<UserDeviceToken>
    {
        public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
        {
            builder.ToTable("UserDeviceTokens");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.DeviceIdentifier)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Platform)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.FcmToken)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.CreatedAtUtc)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            builder.Property(x => x.UpdatedAtUtc)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            builder.Property(x => x.LastSeenAtUtc)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.CreatedDate)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.ModifiedDate)
                .HasColumnType("timestamp with time zone");

            // Indexes for fast lookup & cross-user checks
            builder.HasIndex(x => x.FcmToken)
                .HasDatabaseName("IX_UserDeviceTokens_FcmToken");

            builder.HasIndex(x => new { x.UserId, x.DeviceIdentifier })
                .HasDatabaseName("IX_UserDeviceTokens_UserId_DeviceIdentifier");

            builder.HasIndex(x => new { x.UserId, x.IsActive })
                .HasDatabaseName("IX_UserDeviceTokens_UserId_IsActive");

            builder.HasOne(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
