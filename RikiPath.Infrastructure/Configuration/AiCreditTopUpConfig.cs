using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class AiCreditTopUpConfig : IEntityTypeConfiguration<AiCreditTopUp>
    {
        public void Configure(EntityTypeBuilder<AiCreditTopUp> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PaymentTransactionId).HasMaxLength(200);

            builder.HasOne(x => x.UserAccount)
                .WithMany(x => x.AiCreditTopUps)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.PaymentStatus });
        }
    }
}
