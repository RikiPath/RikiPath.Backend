using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificateLevelConfig : IEntityTypeConfiguration<CertificateLevel>
    {
        public void Configure(EntityTypeBuilder<CertificateLevel> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CertificateTypeId).HasColumnName("CertificationId");
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);

            builder.HasOne(x => x.CertificateType)
                .WithMany(x => x.Levels)
                .HasForeignKey(x => x.CertificateTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificateTypeId, x.Code }).IsUnique();
        }
    }
}
