using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ConsultationRequestConfig : IEntityTypeConfiguration<ConsultationRequest>
    {
        public void Configure(EntityTypeBuilder<ConsultationRequest> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.MeetingLink).HasMaxLength(500);

            builder.HasOne(x => x.ConsultationPurchase)
                .WithOne(x => x.ConsultationRequest)
                .HasForeignKey<ConsultationRequest>(x => x.ConsultationPurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            // 1-to-1, both sides optional: a request may not have booked a slot yet
            // (e.g. WrittenAnswer type never books one), and a slot may not be booked yet.
            builder.HasOne(x => x.ConsultantAvailability)
                .WithOne(x => x.ConsultationRequest)
                .HasForeignKey<ConsultationRequest>(x => x.ConsultantAvailabilityId)
                .OnDelete(DeleteBehavior.SetNull);

            // Consultant side (Restrict) configured in UserConfig.
            builder.HasIndex(x => new { x.ConsultantId, x.Status });
            builder.HasIndex(x => x.ConsultantAvailabilityId).IsUnique();
        }
    }
}
