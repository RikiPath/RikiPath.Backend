using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ConsultationPurchaseConfig : IEntityTypeConfiguration<ConsultationPurchase>
    {
        public void Configure(EntityTypeBuilder<ConsultationPurchase> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PaymentTransactionId).HasMaxLength(200);

            builder.HasOne(x => x.ConsultationPackage)
                .WithMany(x => x.Purchases)
                .HasForeignKey(x => x.ConsultationPackageId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserAccount side (Restrict) configured in UserConfig.
        }
    }
}
