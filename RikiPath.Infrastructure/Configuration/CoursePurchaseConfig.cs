using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Configuration
{
    public class CoursePurchaseConfig : IEntityTypeConfiguration<CoursePurchase>
    {
        public void Configure(EntityTypeBuilder<CoursePurchase> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PaymentTransactionId).HasMaxLength(200);

            builder.HasOne(x => x.UserAccount)
                .WithMany(x => x.CoursePurchases)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Course)
                .WithMany(x => x.Purchases)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // PayOS's webhook only carries orderCode, so this is the lookup key for incoming
            // callbacks — it must be unique per payment link.
            builder.HasIndex(x => x.OrderCode).IsUnique();
            builder.HasIndex(x => new { x.UserId, x.CourseId, x.PaymentStatus });
        }
    }
}
