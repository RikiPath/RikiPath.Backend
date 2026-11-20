using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificateLevelConfig : IEntityTypeConfiguration<CertificateLevel>
    {
        public void Configure(EntityTypeBuilder<CertificateLevel> builder)
        {
            builder.ToTable("CertificateLevels", t =>
            {
                t.HasCheckConstraint("CK_CertificateLevels_SortOrder", "\"SortOrder\" >= 1");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CertificateTypeId).HasColumnName("CertificationId");
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Revision).IsConcurrencyToken().HasDefaultValue(1);

            builder.HasOne(x => x.CertificateType)
                .WithMany(x => x.Levels)
                .HasForeignKey(x => x.CertificateTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificateTypeId, x.Code }).IsUnique();

            builder.HasData(
                new CertificateLevel
                {
                    Id = 1, CertificateTypeId = 1, Code = "N5", Description = "JLPT N5", SortOrder = 1, Revision = 1,
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc), IsDeleted = false
                },
                new CertificateLevel
                {
                    Id = 2, CertificateTypeId = 1, Code = "N4", Description = "JLPT N4", SortOrder = 2, Revision = 1,
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc), IsDeleted = false
                },
                new CertificateLevel
                {
                    Id = 3, CertificateTypeId = 1, Code = "N3", Description = "JLPT N3", SortOrder = 3, Revision = 1,
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc), IsDeleted = false
                },
                new CertificateLevel
                {
                    Id = 4, CertificateTypeId = 1, Code = "N2", Description = "JLPT N2", SortOrder = 4, Revision = 1,
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc), IsDeleted = false
                },
                new CertificateLevel
                {
                    Id = 5, CertificateTypeId = 1, Code = "N1", Description = "JLPT N1", SortOrder = 5, Revision = 1,
                    CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc), IsDeleted = false
                });
        }
    }
}
