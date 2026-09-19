using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificationLevelConfig : IEntityTypeConfiguration<CertificationLevel>
    {
        public void Configure(EntityTypeBuilder<CertificationLevel> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);

            builder.HasOne(x => x.Certification)
                .WithMany(x => x.Levels)
                .HasForeignKey(x => x.CertificationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificationId, x.Code }).IsUnique();
        }
    }
}
